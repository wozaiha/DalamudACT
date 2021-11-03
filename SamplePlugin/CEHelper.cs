using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CEHelper.Util;
using Dalamud;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace CEHelper
{
    public sealed class CEHelper : IDalamudPlugin
    {
        public string Name => "CEHelper";

        public Configuration Configuration;
        private PluginUI PluginUi;
        private Dictionary<uint, uint> pet = new();

        public class ACTBattle
        {
            public ACTBattle(long time1, long time2, Dictionary<string, Dictionary<uint, long>> damage)
            {
                StartTime = time1;
                EndTime = time2;
                Damage = damage;
            }

            public long StartTime;
            public long EndTime;
            public Dictionary<string, Dictionary<uint, long>> Damage;

            public long Duration()
            {
                return (EndTime - StartTime) switch
                {
                    <= 0 => 0, //战斗中
                    _ => EndTime - StartTime //战斗结束
                };
            }
        }

        public List<ACTBattle> Battles = new();


        public CEHelper(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);


            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            DalamudApi.GameNetwork.NetworkMessage += NetWork;

            PluginUi = new PluginUI(this);
        }

        #region OPcode functions

        private struct ActorControl
        {
            public ushort category;
            public ushort padding;
            public uint param1;
            public uint param2;
            public uint param3;
            public uint param4;
            public uint padding1;
            //跳Dot      23:????:0000:0003:伤害:ActorID:0
            //效果结束   21:????:BUFF:0000:ActorID:0:0
        }

        private struct NpcSpawn
        {
            private uint gimmickId; // needs to be existing in the map, mob will snap to it
            private byte u2b;
            private byte u2ab;
            private byte gmRank;
            private byte u3b;

            private byte aggressionMode; // 1 passive, 2 aggressive
            private byte onlineStatus;
            private byte u3c;
            private byte pose;

            private uint u4;

            private long targetId;
            private uint u6;
            private uint u7;
            private long mainWeaponModel;
            private long secWeaponModel;
            private long craftToolModel;

            private uint u14;
            private uint u15;
            private uint bNPCBase;
            public uint bNPCName;
            private uint levelId;
            private uint u19;
            private uint directorId;
            public uint spawnerId;
            private uint parentActorId;
            private uint hPMax;
            private uint hPCurr;
            private uint displayFlags;
            private ushort fateID;
            private ushort mPCurr;
            private ushort unknown1; // 0
            private ushort unknown2; // 0 or pretty big numbers > 30000
            private ushort modelChara;
            private ushort rotation;
            private ushort activeMinion;
            private byte spawnIndex;
            private byte state;
            private byte persistantEmote;
            private byte modelType;
            private byte subtype;
        }

        private unsafe (uint,uint,long) Spawn(IntPtr ptr, uint target)
        {
            var obj = (NpcSpawn*)ptr;
            if (pet.ContainsKey(target)) pet[target] = obj->spawnerId;
            else pet.Add(target, obj->spawnerId);
            //PluginLog.Information($"{target:X}:{obj->bNPCName}:{obj->spawnerId:X}");
            return (0xFFFFFFFF,0,0);
        }


        private unsafe (uint,uint,long) DOT(IntPtr ptr,uint target)
        {
            var dat = Marshal.PtrToStructure<ActorControl>(ptr);
            if (dat.category != 23 || dat.param2 != 3) return (0xE000_0000, 0xE000_0000, 0);
            long damage = 0;
            
            if (target > 0x40000000)
            {
                PluginLog.Information($"{target:X}:{dat.category}:{dat.padding:D5}:{dat.param1}:{dat.param2}:{dat.param3}:{dat.param4:X}:{dat.padding1:X}");
                damage = dat.param3;
            }

            return (0xE000_0000,0xE000_0000,damage);
        }


        private unsafe (uint,uint, long) AOE(IntPtr ptr, uint target, int length)
        {
            var index = 0;
            while (*(long*)(ptr + length * 64 + 48 + index * 8) != 0x0000_0000_0000_0000 && index < length) index++;
            //PluginLog.Information(index.ToString());
            var actionId = *(uint*)(ptr + 8);
            long damage = 0;
            for (var i = 0; i < index; i++)
            {
                if (index == 0 || Marshal.ReadByte(ptr, i * 64 + 42) != 3) continue;
                damage  += (*(byte*)(ptr + i * 64 + 46) << 16) + *(ushort*)(ptr + i * 64 + 48);
                //PluginLog.Information((*(long*)(ptr + 560 + i * 8)).ToString("X") +" "+(*(byte*)(ptr+i*64+46)<<16)+*(ushort*)(ptr+i*64+48));
            }

            return (target,actionId,damage);
        }

        private (uint,uint, long) Single(IntPtr ptr, uint target)
        {
            var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            var actionID = (uint)Marshal.ReadInt32(ptr, 8);
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            //var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadByte(ptr, 46) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);
            if (effecttype != 3) damage = 0;
            //PluginLog.Information($"@{actionID}:{effecttype:X}:{direct}:{damage}:{targetID:X}");

            return (target,actionID,(long)damage);
        }

        private void SearchForPet()
        {
            pet.Clear();
            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj == null) continue;
                if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
                var owner = ((BattleNpc)obj).OwnerId;
                if (owner == DalamudApi.ClientState.LocalPlayer?.ObjectId ||
                    DalamudApi.PartyList.Any(x => x.ObjectId == owner))
                {
                    if (pet.ContainsKey(owner)) pet[owner] = obj.ObjectId;
                    else pet.Add(obj.ObjectId, owner);
                    PluginLog.Information($"{owner:X} {obj.ObjectId:X}");
                }
            }
        }

        #endregion

        private void AddDamage(string name, uint action,long value)
        {
            if (Battles[^1].Damage.ContainsKey(name))
            {
                if (Battles[^1].Damage[name].ContainsKey(action)) Battles[^1].Damage[name][action] += value;
                else Battles[^1].Damage[name].Add(action,value);
                Battles[^1].Damage[name][0] += value;   //总伤害
            }
            else
            {
                Battles[^1].Damage.Add(name, new Dictionary<uint, long>{ { action, value } });
                Battles[^1].Damage[name].Add(0,value);
            }
        }

        private void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            if (Battles.Count == 0)
                Battles.Add(new ACTBattle(0,
                    0, new Dictionary<string, Dictionary<uint, long>>()));

            if (direction != NetworkMessageDirection.ZoneDown) return;

            var time = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (DalamudApi.ClientState.LocalPlayer != null &&
                (DalamudApi.ClientState.LocalPlayer.StatusFlags & StatusFlags.InCombat) != 0 &&
                Battles[^1].StartTime == 0) //开始战斗
            {
                Battles[^1].StartTime = time;
                SearchForPet();
                PluginUi.choosed = Battles.Count - 1;
            }


            //PluginLog.Log(opCode.ToString("X"));
            var (source,actionId,damage) = opCode switch
            {
                0x6E => Single(dataPtr, targetActorId),
                0x1D8 => DOT(dataPtr,targetActorId),
                0x3C0 => AOE(dataPtr, targetActorId, 8),
                0x0D2 => AOE(dataPtr, targetActorId, 16),
                0x242 => Spawn(dataPtr, targetActorId),
                _ => (0xFFFFFFFF,(uint)0,0)
            };
            if (source == 0xFFFFFFFF || actionId == 0 || damage == 0) return;

                if (Battles[^1].Duration() > 1 && time - Battles[^1].EndTime > 10) //下一场战斗
                {
                    Battles.Add(new ACTBattle(0,
                        0, new Dictionary<string, Dictionary<uint, long>>()));
                    if (Battles.Count > 3) Battles.RemoveAt(0);
                    Battles[^1].StartTime = time;
                    SearchForPet();
                    PluginUi.choosed = Battles.Count - 1;
                }

                if (pet.ContainsKey(source)) source = pet[source]; //来源是宠物，替换成owner
                var member = DalamudApi.PartyList.FirstOrDefault(x => x.ObjectId == source);
                if (member == default)
                {
                    if (DalamudApi.ClientState.LocalPlayer != null &&
                        source == DalamudApi.ClientState.LocalPlayer.ObjectId)
                        AddDamage(DalamudApi.ClientState.LocalPlayer.Name.TextValue,actionId,damage);
                    if (source == 0xE000_0000 && actionId == 0xE000_0000) AddDamage("Dot", actionId,damage);
                }
                else
                {
                    AddDamage(member.Name.TextValue, actionId,damage);
                }
            
        }


        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
            PluginUi.Dispose();
            DalamudApi.Dispose();
        }

        [Command("/cehelper")]
        [HelpMessage("Show config window of CEHelper.")]
        private void ToggleConfig(string command, string args)
        {
            PluginUi.DrawConfigUI();
        }
    }
}