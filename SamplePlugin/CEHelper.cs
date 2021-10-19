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
        private Dictionary<uint,uint> pet = new();

        public class ACTBattle
        {
            public ACTBattle(long time1, long time2, Dictionary<string, long> damage)
            {
                StartTime = time1;
                EndTime = time2;
                Damage = damage;
            }

            public long StartTime;
            public long EndTime;
            public Dictionary<string, long> Damage;

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

        struct ActorControl
        {
            public ushort category;
            ushort padding;
            public uint param1;
            public uint param2;
            public uint param3;
            public uint param4;

            uint padding1;
            //跳Dot      23:????:0000:0003:伤害:ActorID:0
            //效果结束   21:????:BUFF:0000:ActorID:0:0
        }

        struct NpcSpawn
        {
            uint gimmickId; // needs to be existing in the map, mob will snap to it
            byte u2b;
            byte u2ab;
            byte gmRank;
            byte u3b;

            byte aggressionMode; // 1 passive, 2 aggressive
            byte onlineStatus;
            byte u3c;
            byte pose;

            uint u4;

            long targetId;
            uint u6;
            uint u7;
            long mainWeaponModel;
            long secWeaponModel;
            long craftToolModel;

            uint u14;
            uint u15;
            uint bNPCBase;
            public uint bNPCName;
            uint levelId;
            uint u19;
            uint directorId;
            public uint spawnerId;
            uint parentActorId;
            uint hPMax;
            uint hPCurr;
            uint displayFlags;
            ushort fateID;
            ushort mPCurr;
            ushort unknown1; // 0
            ushort unknown2; // 0 or pretty big numbers > 30000
            ushort modelChara;
            ushort rotation;
            ushort activeMinion;
            byte spawnIndex;
            byte state;
            byte persistantEmote;
            byte modelType;
            byte subtype;
        }

        unsafe Dictionary<uint, long> Spawn(IntPtr ptr, uint target)
        {
            var obj = (NpcSpawn*)ptr;
            if (pet.ContainsKey(obj->spawnerId)) pet[obj->spawnerId] = target;
            else pet.Add(target,obj->spawnerId);
            //PluginLog.Information($"{target:X}:{obj->bNPCName}:{obj->spawnerId:X}");
            return new Dictionary<uint, long>();
        }


        Dictionary<uint, long> DOT(IntPtr ptr)
        {
            var dat = Marshal.PtrToStructure<ActorControl>(ptr);
            var dic = new Dictionary<uint, long>();
            if (dat.category == 23 && dat.param2 == 3)
            {
                dic.Add(0xE000_0000, dat.param3);
            }

            return dic;
        }


        unsafe Dictionary<uint, long> AOE(IntPtr ptr, uint target)
        {
            var index = 0;
            while (*(long*)(ptr + 562 + index * 8) != 0x0000_0000_0000_0000) index++;
            var dic = new Dictionary<uint, long> { { target, 0 } };
            for (var i = 0; i < index; i++)
            {
                if (index == 0 || Marshal.ReadByte(ptr, i * 64 + 42) != 3) continue;
                dic[target] += (*(byte*)(ptr + i * 64 + 46) << 16) + *(ushort*)(ptr + i * 64 + 48);
                //PluginLog.Information((*(long*)(ptr + 560 + i * 8)).ToString("X") +" "+(*(byte*)(ptr+i*64+46)<<16)+*(ushort*)(ptr+i*64+48));
            }

            return dic;
        }

        Dictionary<uint, long> Single(IntPtr ptr, uint target)
        {
            var dic = new Dictionary<uint, long>();
            var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            var actionID = (uint)Marshal.ReadInt32(ptr, 8);
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadByte(ptr, 46) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);

            if (effecttype == 3) dic.Add(target, damage);
            //PluginLog.Information($"@{actionID}:{effecttype:X}:{direct}:{damage}:{targetID:X}");
            
            return dic;
        }

        void SearchForPet()
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
                 else pet.Add(obj.ObjectId,owner);
                 PluginLog.Information($"{owner:X} {obj.ObjectId:X}");
             }
            }
        }

        
        #endregion

        void AddDamage(string name, long value)
        {
            if (Battles[^1].Damage.ContainsKey(name)) Battles[^1].Damage[name] += value;
            else Battles[^1].Damage.Add(name, value);
        }

        void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            if (Battles.Count == 0)
                Battles.Add(new ACTBattle(0,
                    0, new Dictionary<string, long>()));

            if (direction != NetworkMessageDirection.ZoneDown) return;

            var time = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (DalamudApi.ClientState.LocalPlayer != null && (DalamudApi.ClientState.LocalPlayer.StatusFlags & StatusFlags.InCombat) != 0 &&
                Battles[^1].StartTime == 0)     //开始战斗
            {
                Battles[^1].StartTime = time;
                SearchForPet();
            }



            //PluginLog.Log(opCode.ToString("X"));
                var dic = opCode switch
            {
                0x6E => Single(dataPtr, targetActorId),
                0x1D8 => DOT(dataPtr),
                0x3C0 => AOE(dataPtr, targetActorId),
                0x242 => Spawn(dataPtr,targetActorId),
                _ => null
            };
            if (dic == null) return;
            
            foreach (var (key0, value) in dic)
            {
                var key = key0;
                //PluginLog.Log($"{key:X} {value}");
                if (Battles[^1].Duration() > 1 && time - Battles[^1].EndTime >10)    //下一场战斗
                {
                    Battles.Add(new ACTBattle(0,
                        0, new Dictionary<string, long>()));
                    if (Battles.Count > 3) Battles.RemoveAt(0);
                    Battles[^1].StartTime = time;
                    SearchForPet();
                }

                if (pet.ContainsKey(key)) key = pet[key];   //来源是宠物，替换成owner
                var member = DalamudApi.PartyList.FirstOrDefault(x => x.ObjectId == key);
                if (member == default)
                {
                    if (DalamudApi.ClientState.LocalPlayer!= null && key == DalamudApi.ClientState.LocalPlayer.ObjectId)
                        AddDamage(DalamudApi.ClientState.LocalPlayer.Name.TextValue, value);
                    if (key == 0xE000_0000) AddDamage("Dot", value);
                }
                else
                {
                    AddDamage(member.Name.TextValue,value);
                }
               
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