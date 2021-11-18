using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ACT.Struct;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace ACT
{
    public class ACT : IDalamudPlugin
    {
        public string Name => "ACT";

        public Configuration Configuration;
        private PluginUI PluginUi;
        private Dictionary<uint, uint> pet = new();
        public Dictionary<uint, TextureWrap?> Icon = new();
        public List<ACTBattle> Battles = new(5);
        private ExcelSheet<TerritoryType> terrySheet;
        public static ExcelSheet<Action> sheet;
        public class ACTBattle
        {
            public ACTBattle(long time1, long time2)
            {
                StartTime = time1;
                EndTime = time2;
                Level = DalamudApi.ClientState.LocalPlayer?.Level;
            }

            public class Dot
            {
                public uint Target;
                public uint Source;
                public uint BuffId;
                
            }

            public class Damage
            {
                public Dictionary<uint, long> Damages = new();
                public uint PotSkill;
                public float SkillPotency;
                public uint JobId;
                public float Speed = 1f;
                public float Special = 1f;
            }

            public long StartTime;
            public long EndTime;
            public string? Zone;
            public int? Level;
            public Dictionary<uint, string> Name = new();
            public Dictionary<uint, Damage> DamageDic = new();

            public Dictionary<ulong, uint> PlayerDotPotency = new();

            public HashSet<BigInteger> ActiveDots = new();
            public long TotalDotDamage;



            public float Duration()
            {
                return (EndTime - StartTime) switch
                {
                    <= 0 => 0.01f, //战斗中
                    _ => EndTime - StartTime //战斗结束
                };
            }

            private void AddPlayer(uint objectId)
            {
                var actor = DalamudApi.ObjectTable.FirstOrDefault(x => x.ObjectId == objectId && x.ObjectKind == ObjectKind.Player);
                if (actor == default || actor.Name.TextValue == "") return; 
                Name.Add(objectId,actor.Name.TextValue);
                DamageDic.Add(objectId,new Damage());
                DamageDic[objectId].Damages = new Dictionary<uint, long> { { 0, 0 } };
                DamageDic[objectId].PotSkill = 0;
                //DamageDic[objectId].PotSwings = 0;
                DamageDic[objectId].SkillPotency = 0;
                //DamageDic[objectId].DPP = 0;
                DamageDic[objectId].JobId = ((Character)actor).ClassJob.Id;
                DamageDic[objectId].Speed = 1f;

            }

            public void AddSS(uint objectId,float casttime,uint actionId)
            {
                var muti = actionId switch
                {
                    7 => 1,
                    8 => 1,
                    3598 => 1.5f/casttime,
                    7442 => 1.5f/casttime,
                    16555 =>1.5f/casttime,
                    3577 => 2.8f/casttime,
                    _ =>  2.5f/casttime,
                };
                if (DamageDic.ContainsKey(objectId) && (DamageDic[objectId].Speed > muti || DamageDic[objectId].Speed == 1)) DamageDic[objectId].Speed = muti;
            }
            
            public void AddEvent(int kind, uint from, uint target, uint id, long damage)
            {
                if (from > 0x40000000 && from != 0xE0000000)
                {
                    PluginLog.Error($"Unknown Id {from:X}");
                    return;
                }
                if (!Name.ContainsKey(from)) AddPlayer(from);
                //PluginLog.Log($"{Name.Count}");

                //DOT 伤害
                if (from == 0xE0000000 && kind == 3)
                {
                    if (!CheckTargetDot(target)) return;
                    TotalDotDamage += damage;
                    foreach (var active in ActiveDots)
                    {
                        var dot = ActiveToDot(active);
                        var buff = ActiveToUlong(active);
                        if (PlayerDotPotency.ContainsKey(buff)) PlayerDotPotency[buff] += Potency.DotPot[dot.BuffId] * (uint)DamageDic[dot.Source].Special;
                        else PlayerDotPotency.Add(buff,Potency.DotPot[dot.BuffId] * (uint)DamageDic[dot.Source].Special);
                    }
                }

                //伤害
                if (from != 0xE0000000 && kind == 3) 
                {
                    if (Potency.SkillPot.TryGetValue(id, out var pot))  //基线技能
                    {
                        if (DamageDic[from].PotSkill == 0)
                        {
                            DamageDic[from].PotSkill = id;
                            //DamageDic[from].PotSwings = 1;
                            DamageDic[from].SkillPotency = pot * Potency.Muti[DamageDic[from].JobId];
                        }
                        else
                        {
                            DamageDic[from].SkillPotency += pot * Potency.Muti[DamageDic[from].JobId];
                        }
                    }

                    if (DamageDic[from].Damages.TryGetValue(id, out var dmg))
                    {
                        dmg += damage;
                        DamageDic[from].Damages[id] = dmg;
                    }
                    else
                    {
                        DamageDic[from].Damages.Add(id,damage);
                        PluginUI.Icon.TryAdd(id,DalamudApi.DataManager.GetImGuiTextureHqIcon(sheet.GetRow(id)!.Icon));
                    }

                    DamageDic[from].Damages[0] += damage;
                }

                ////BUFF up
                //if (kind == 0xE && target > 0x40000000 && Potency.DotPot.ContainsKey(id))
                //{
                //    var dot = DotToActive(new Dot{ BuffId = id, Source = from, Target = target });
                //    if (!ActiveDots.Contains(dot)) ActiveDots.Add(dot);
                //}
                
                ////Buff gone
                //if (kind == 21 && target > 0x40000000 && Potency.DotPot.ContainsKey(id))
                //{
                    
                //    var dot = DotToActive(new Dot{ BuffId = id, Source = from, Target = target });
                //    if (ActiveDots.Contains(dot)) ActiveDots.Remove(dot);
                //}

            }

            public static ulong ActiveToUlong(BigInteger active)
            {
                return (ulong)(active >> 32);
            }
            
            public static Dot ActiveToDot(BigInteger active)
            {
                return new Dot()
                {
                    BuffId = (uint)(active >> 64), 
                    Source = (uint)(active >> 32 &0xFFFFFFFF),
                    Target = (uint)(active & 0xFFFFFFFF)
                };
            }

            private static BigInteger DotToActive(Dot dot)
            {
                return ((BigInteger)dot.BuffId << 64) + ((BigInteger)dot.Source << 32) + dot.Target ;
            }

            public float PDD(uint actor)
            {
                return DamageDic[actor].Damages[DamageDic[actor].PotSkill] * DamageDic[actor].Speed / DamageDic[actor].SkillPotency;

            }

            private bool CheckTargetDot(uint id)
            {
                var target = DalamudApi.ObjectTable.SearchById(id);
                if (target == null || target.ObjectKind != ObjectKind.BattleNpc)
                {
                    PluginLog.Error($"Dot target {id:X} is not BattleNpc");
                    return false;
                }
                ActiveDots.Clear();
                var npc = (BattleNpc)target;
                foreach (var status in npc.StatusList)
                {
                    if (Potency.DotPot.ContainsKey(status.StatusId))
                    {
                        ActiveDots.Add(DotToActive(new Dot(){BuffId = status.StatusId,Source = status.SourceID,Target = id}));
                    }
                }

                return true;
            }
        }

        #region OPcode functions
        
        private unsafe void Spawn(IntPtr ptr, uint target)
        {
            var obj = (NpcSpawn*)ptr;
            if (pet.ContainsKey(target)) pet[target] = obj->spawnerId;
            else pet.Add(target, obj->spawnerId);
            PluginLog.Debug($"{target:X}:{obj->bNPCName}:{obj->spawnerId:X}");
        }

        private void ActorControl(IntPtr ptr,uint target)
        {
            if (target < 0x40000000) return;
            
            var dat = Marshal.PtrToStructure<ActorControl.ActorControlStruct>(ptr);

            PluginLog.Debug($"ActorControl {target:X}:{dat.category}:{dat.padding:D5}:{dat.param1}:{dat.type}:{dat.param3}:{dat.param4:X}:{dat.padding1:X}");
            if (dat.category == 23 && dat.type == 3)
            {
                if (dat.param1 != 0)
                {
                    if (Potency.BuffToAction.TryGetValue(dat.param1, out var actionId))
                    {
                        if (dat.param4 > 0x40000000) pet.TryGetValue(dat.param4, out dat.param4);
                        Battles[^1].AddEvent(3, dat.param4, target, actionId, dat.param3);
                    }
                        
                }
                else Battles[^1].AddEvent(3, 0xE000_0000, target, 0, dat.param3);
            }
            //else if (dat.category == 21 && dat.type == 0)
            //{
            //    Battles[^1].AddEvent(21, dat.param3, target, dat.param1, 0);
            //}
        }

        private unsafe void Ability(IntPtr ptr, uint sourceId, int length)
        {
            if (sourceId > 0x40000000) pet.TryGetValue(sourceId, out sourceId);
            if (sourceId > 0x40000000) return;

            var header = Marshal.PtrToStructure<Header>(ptr);
            var effect = (EffectEntry*)(ptr + sizeof(Header));
            var target = (ulong*)(ptr + sizeof(Header) + 8*sizeof(EffectEntry)*length);
            PluginLog.Debug($"-----------------------Ability{length}------------------------------");
            for (int i = 0; i < length; i++)
            {
                if (*target == 0x0) break;
                //PluginLog.Debug($"{*target:X}");
                for (int j = 0; j < 8; j++)
                {
                    if (effect->type == 3)
                    {
                        long damage = effect->param0;
                        if (effect->param5 == 0x40) damage += effect->param4 << 16;
                        Battles[^1].AddEvent(3,sourceId,(uint)*target,header.actionId,damage);
                        PluginLog.Debug($"{3},{sourceId:X}:{(uint)*target}:{header.actionId},{damage}");
                    }
                    //else if (effect->type == 0xE)
                    //{
                    //    Battles[^1].AddEvent(0xE,sourceId,(uint)data.targetId[i],effect->param0,0);
                    //}
                    effect++;
                }
                target++;
            }
            PluginLog.Debug("------------------------END------------------------------");
        }
        
        private void Cast(IntPtr ptr, uint source)
        {
            if (source>0x40000000) return;
            var data = Marshal.PtrToStructure<ActorCast>(ptr);
            PluginLog.Debug($"Cast:{data.skillType}:{data.action_id}:{data.cast_time}");
            if (data.skillType == 1 && Potency.SkillPot.ContainsKey(data.action_id))
            {
                if (Battles[^1].DamageDic.TryGetValue(source, out var damage))
                {
                    Battles[^1].AddSS(source,data.cast_time,data.action_id); 
                }

                if (data.action_id == 3577) //火3 天语
                {
                    Battles[^1].DamageDic[source].Special = DalamudApi.ClientState.LocalPlayer?.Level switch
                    {
                        >=78 => 1.15f,
                        >=56 => 1.10f,
                        _ => 1f
                    };
                }
            }

            if (data.action_id == 7489) //彼岸花 回天
            {
                var actor = (PlayerCharacter)DalamudApi.ObjectTable.First(x =>
                    x.ObjectId == source && x.ObjectKind == ObjectKind.Player);
                Battles[^1].DamageDic[source].Special = actor.StatusList.Any(x => x.StatusId == 1229) ? 1.5f : 1.0f;
            }

        }

        private void SearchForPet()
        {
            pet.Clear();
            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj == null) continue;
                if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
                var owner = ((BattleNpc)obj).OwnerId;

                if (pet.ContainsKey(owner)) pet[owner] = obj.ObjectId;
                    else pet.Add(obj.ObjectId, owner);
                    //PluginLog.Information($"{owner:X} {obj.ObjectId:X}");
                
            }
        }

        #endregion

        private void CheckTime()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Battles[^1].EndTime > 0 && now - Battles[^1].EndTime > 10)
            {
                Battles[^1].ActiveDots.Clear();
                //新的战斗
                if (Battles.Count == 3) Battles.RemoveAt(0);
                Battles.Add(new ACTBattle(0,0));
                SearchForPet();
            }
            if (Battles[^1].StartTime is 0 && Battles[^1].EndTime is 0)
            {
                if (DalamudApi.ClientState.LocalPlayer != null && (DalamudApi.ClientState.LocalPlayer?.StatusFlags & StatusFlags.InCombat) != 0)
                {
                    //开始战斗
                    Battles[^1].StartTime = now;
                    Battles[^1].EndTime = now;
                    Battles[^1].Zone = terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceName.Value.Name;
                    PluginUi.choosed = Battles.Count - 1;
                    SearchForPet();
                }
            }
            
        }

        private void NetWork(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction)
        {
            CheckTime();
            //PluginLog.Debug(opCode.ToString("X"));
            switch (opCode)
            {
                case 0x032E: 
                    Ability(dataPtr, targetActorId,1);
                    break;
                case 0x20D:
                    Ability(dataPtr, targetActorId, 8);
                    break;
                case 0x0DF:
                    Ability(dataPtr, targetActorId, 16);
                    break;
                case 0x00CA: 
                    ActorControl(dataPtr, targetActorId);
                    break;
                case 0x3B4:
                    Spawn(dataPtr, targetActorId);
                    break;
                case 0x0116:
                    Cast(dataPtr, targetActorId);
                    break;
            }
            
        }


        public ACT(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);

            for (uint i = 62100; i < 62141; i++)
            {
                Icon.Add(i-62100,DalamudApi.DataManager.GetImGuiTextureHqIcon(i));
            }
            Icon.Add(99,DalamudApi.DataManager.GetImGuiTextureHqIcon(103)); //LB

            Battles.Add(new ACTBattle(0,0));
            
            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            terrySheet = DalamudApi.DataManager.GetExcelSheet<TerritoryType>();
            sheet = DalamudApi.DataManager.GetExcelSheet<Action>();

            DalamudApi.GameNetwork.NetworkMessage += NetWork;

            PluginUi = new PluginUI(this);
        }
        public void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetWork;
            PluginUi?.Dispose();
            foreach (var (id,texture) in Icon)
            {
                texture?.Dispose();
            }
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