using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ACT.Struct;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiScene;

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

            public class Dot
            {
                public uint Target;
                public uint Source;
                public uint BuffId;
                
            }

            public class Damage
            {
                public Dictionary<uint, long>? Damages;
                public uint PotSkill;
                public uint PotSwings;
                public long PotDamage;
                public float DPP;//每Pot伤害
                public uint JobId;
            }

            public long StartTime;
            public long EndTime;
            public string? Zone;
            public int? Level;
            public Dictionary<uint, string> Name;
            public Dictionary<uint, Damage> DamageDic;

            public Dictionary<Dot, uint> PlayerDotTicks;

            public HashSet<Dot> ActiveDots;
            private long TotalDotDamage;
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
                    TotalDotDamage += damage;
                    foreach (var dot in ActiveDots.Where(dot => dot.Target == target))
                    {
                        TotalDotPot += Potency.DotPot[dot.BuffId];
                        if (PlayerDotTicks.ContainsKey(dot)) PlayerDotTicks[dot] += 1;
                        else PlayerDotTicks.Add(dot,1);
                    }
                }

                if (from != 0xE0000000 && kind == 3) //伤害
                {

                }
            }

        }

        public List<ACTBattle> Battles = new();


        

        #region OPcode functions
        

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
            var dat = Marshal.PtrToStructure<ActorControl.ActorControlStruct>(ptr);
            PluginLog.Information($"ActorControl {target:X}:{dat.category}:{dat.padding:D5}:{dat.param1}:{dat.type}:{dat.param3}:{dat.param4:X}:{dat.padding1:X}");
            if (dat.category != 23 || dat.type != 3) return (0xE000_0000, 0xE000_0000, 0);
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

            var data = Marshal.PtrToStructure<Ability8>(ptr);
            var effect = (EffectEntry*)data.enrty;
            PluginLog.Information("-----------------------AOE8------------------------------");
            for (int i = 0; i < 64; i++)
            {
                PluginLog.Information($"{effect->type:X}:{effect->param1:X}:{effect->param2:X}:{effect->param3:X}:{effect->param4:X}:{effect->param5:X}:{effect->param0:X}");

                effect += 1;
            }
            
            PluginLog.Information($"{data.targetId[0]:X}:{data.targetId[1]:X}");
            PluginLog.Information("------------------------END------------------------------");

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

        private unsafe (uint,uint, long) Effect(IntPtr ptr, uint target)
        {
            
            var data = Marshal.PtrToStructure<Ability1>(ptr);
            var effect = (EffectEntry*)data.enrty;
            PluginLog.Information("-----------------------ABI1------------------------------");
            for (int i = 0; i < 8; i++)
            {
                PluginLog.Information($"{effect->type:X}:{effect->param1:X}:{effect->param2:X}:{effect->param3:X}:{effect->param4:X}:{effect->param5:X}:{effect->param0:X}");

                effect += 1;
            }
            
            PluginLog.Information($"{data.targetId:X8}");
            PluginLog.Information("------------------------END------------------------------");

            var targetID = (uint)Marshal.ReadInt32(ptr, 112);
            var actionID = (uint)Marshal.ReadInt32(ptr, 8); 
            var effecttype = Marshal.ReadByte(ptr, 42); //03=伤害 0xE=BUFF 04=治疗
            var direct = Marshal.ReadByte(ptr, 43); // 1=暴击  2=直击  3=直爆
            var damage = (Marshal.ReadInt16(ptr, 45) << 16) + (ushort)Marshal.ReadInt16(ptr, 48);
            PluginLog.Information($"Effect@{actionID}:{effecttype:X}:{direct}:{damage:X}:{targetID:X}");
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

        

        private void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            

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
            
            
        }


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

            //PluginUi = new PluginUI(this);
        }
        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
            //PluginUi?.Dispose();
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