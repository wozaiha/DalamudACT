using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace ACT
{
    public class ACT : IDalamudPlugin
    {
        public string Name => "ACT";

        public Configuration Configuration;
        private PluginUI PluginUi;
        private Dictionary<uint, uint> pet = new();
        private Dictionary<uint, TextureWrap?> Icon = new();
        public class ACTBattle
        {
            public ACTBattle(long time1, long time2)
            {
                StartTime = time1;
                EndTime = time2;
                Level = DalamudApi.ClientState.LocalPlayer?.Level;
                //PotDictionary = new Dictionary<uint, float>();
            }

            public long StartTime;
            public long EndTime;
            public string? Zone;
            public int? Level;
            public Dictionary<uint, string> Name;
            public Dictionary<uint, Dictionary<uint, long>> Damage;
            public Dictionary<uint, uint> JobId;
            public Dictionary<uint, float> PotDictionary;
            public Dictionary<uint, long> PotDamage;
            public Dictionary<uint, uint> Swing;

            public Dictionary<ulong, uint> PlayerDotTicks;//from+buffid,tick

            //public Dictionary<ulong,uint> AllDots;      
            public Dictionary<ulong,uint> ActiveDots;   //target+buffid,from
            private long TotalDot;
            private long TotalDotPot;

            public long Duration()
            {
                return (EndTime - StartTime) switch
                {
                    <= 0 => 0, //战斗中
                    _ => EndTime - StartTime //战斗结束
                };
            }

            public bool ContainPlayer(uint ObjectId)
            {
                return Name.ContainsKey(ObjectId);
            }

            public void AddEvent(int kind, uint from, uint target, uint id, long damage)
            {
                if (from == 0xE0000000 && kind == 3)//DOT damage
                {
                    TotalDot += damage;
                    foreach (var (dot,player) in ActiveDots)
                    {
                        if (dot >> 16 != target) continue;
                        TotalDotPot += Potency.DotPot[(uint)dot & 0xFFFF];
                        var playerbuffid = dot & 0xFFFF + player << 16;
                        if (PlayerDotTicks.ContainsKey(playerbuffid)) PlayerDotTicks[playerbuffid] += 1;
                        else PlayerDotTicks.Add(playerbuffid,1);
                    }
                }

                if (from != 0xE0000000 && kind == 3) //伤害
                {

                }
            }

        }

        public List<ACTBattle> Battles = new();


        public ACT(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);

            for (uint i = 62100; i < 62141; i++)
            {
                Icon.Add(i-62100,DalamudApi.DataManager.GetImGuiTextureHqIcon(i));
            }
            Icon.Add(99,DalamudApi.DataManager.GetImGuiTextureHqIcon(103)); //LB
            
            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            DalamudApi.GameNetwork.NetworkMessage += NetWork;

            PluginUi = new PluginUI(this);
        }

        #region OPcode functions

        private struct ActorControlStruct
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


        private (uint,uint,long) ActorControl(IntPtr ptr,uint target)
        {
            var dat = Marshal.PtrToStructure<ActorControlStruct>(ptr);
            PluginLog.Information($"ActorControl {target:X}:{dat.category}:{dat.padding:D5}:{dat.param1}:{dat.param2}:{dat.param3}:{dat.param4:X}:{dat.padding1:X}");
            if (dat.category != 23 || dat.param2 != 3) return (0xE000_0000, 0xE000_0000, 0);
            long damage = 0;
            
            if (target > 0x40000000)
            {
                //PluginLog.Information($"{target:X}:{dat.category}:{dat.padding:D5}:{dat.param1}:{dat.param2}:{dat.param3}:{dat.param4:X}:{dat.padding1:X}");
                damage = dat.param3;
            }

            return dat.param1 == 861 ? (dat.param4, 2878, damage) : (0xE000_0000,0xE000_0000,damage);//野火
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

        private (uint,uint, long) Effect(IntPtr ptr, uint target)
        {
            //var str = "";
            //for (int i = 0; i < 6; i++)
            //{
            //    for (int j = 0; j < 100; j++)
            //    {
            //        str += Marshal.ReadByte(ptr, i * 100 + j).ToString("X2") + " ";
            //    }
            //    PluginLog.Information(str);
            //    str = "";
            //}
            //PluginLog.Information("END");

            var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            var actionID = (uint)Marshal.ReadInt32(ptr, 8); 
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadInt16(ptr, 45) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);
            PluginLog.Information($"Effect@{actionID}:{effecttype:X}:{direct}:{damage}:{targetID:X}");
                                           //{rotateId}      {0xE}        {???} {buffId}  {targetId}:
            if (effecttype != 3) damage = 0;
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
                    //PluginLog.Information($"{owner:X} {obj.ObjectId:X}");
                }
            }
        }

        #endregion

        private void AddDamage(string name, uint action,long value,uint iconid)
        {
            if (Battles[^1].Damage.ContainsKey(name)) //已有key
            {
                if (Battles[^1].Damage[name].ContainsKey(action)) Battles[^1].Damage[name][action] += value;
                else Battles[^1].Damage[name].Add(action,value);
                Battles[^1].Damage[name][0] += value;   //总伤害
            }
            else
            {   //新建
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
                Battles[^1].Zone = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()
                    .GetRow(DalamudApi.ClientState.TerritoryType).PlaceName.Value.Name;
                SearchForPet();
                PluginUi.choosed = Battles.Count - 1;
            }


            //PluginLog.Log(opCode.ToString("X"));
            var (source,actionId,damage) = opCode switch
            {
                0x032E => Effect(dataPtr, targetActorId),           //Effect
                0x00CA => ActorControl(dataPtr,targetActorId),         //ActorControl
                0x20D => AOE(dataPtr, targetActorId, 8),      //AOE8
                0x0DF => AOE(dataPtr, targetActorId, 16),       //AOE16
                0x3B4 => Spawn(dataPtr, targetActorId),             //NPCSpawn
                _ => (0xFFFFFFFF,(uint)0,0)
            };
            if (source == 0xFFFFFFFF || actionId == 0 || damage == 0) return;

                if (Battles[^1].Duration() > 1 && time - Battles[^1].EndTime > 10) //下一场战斗
                {
                    Battles.Add(new ACTBattle(0,
                        0, new Dictionary<string, Dictionary<uint, long>>()));
                    if (Battles.Count > 3) Battles.RemoveAt(0);
                    Battles[^1].StartTime = time;
                    Battles[^1].Zone = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()
                        .GetRow(DalamudApi.ClientState.TerritoryType).PlaceName.Value.Name;
                    SearchForPet();
                    PluginUi.choosed = Battles.Count - 1;
                }

                if (pet.ContainsKey(source)) source = pet[source]; //来源是宠物，替换成owner
                var member = DalamudApi.PartyList.FirstOrDefault(x => x.ObjectId == source);
                if (member == default)
                {
                    if (DalamudApi.ClientState.LocalPlayer != null &&
                        source == DalamudApi.ClientState.LocalPlayer.ObjectId)
                        AddDamage(DalamudApi.ClientState.LocalPlayer.Name.TextValue,actionId,damage,DalamudApi.ClientState.LocalPlayer.ClassJob.Id+62100);
                    if (source == 0xE000_0000 && actionId == 0xE000_0000) AddDamage("Dot", actionId,damage,0);
                }
                else
                {
                    AddDamage(member.Name.TextValue, actionId,damage,member.ClassJob.Id+62100);
                }
            
        }


        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
            PluginUi.Dispose();
            DalamudApi.Dispose();
        }

        //[Command("/cehelper")]
        //[HelpMessage("Show config window of CEHelper.")]
        private void ToggleConfig(string command, string args)
        {
            PluginUi.DrawConfigUI();
        }
    }
}