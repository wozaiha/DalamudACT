using System.Collections;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACT
{

    public class ACTBattle
    {
        public static ExcelSheet<Action> ActionSheet;
        public static Dictionary<uint, uint> pet = new();

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

        public class Data
        {
            public Dictionary<uint, SkillDamage> Damages = new();
            public uint PotSkill;
            public float SkillPotency;
            public uint JobId;
            public float Speed = 1f;
            public float Special = 1f;
        }

        public class SkillDamage
        {
            public long Damage=0;
            public uint D=0;
            public uint C=0;
            public uint DC=0;
            public uint swings=0;

            public SkillDamage(long damage = 0)
            {
                Damage = damage;
            }

            public void AddDC(byte dc)
            {
                switch (dc)
                {
                    case 1:
                        D++;
                        break;
                    case 2:
                        C++;
                        break;
                    case 3:
                        DC++;
                        break;
                }
                swings++;
            }

            public void AddDamage(long damage)
            {
                Damage += damage;
            }
        }

        public long StartTime;
        public long EndTime;
        public string? Zone;
        public int? Level;
        public Dictionary<uint, string> Name = new();
        public Dictionary<uint, Data> DataDic = new();

        public Dictionary<ulong, uint> PlayerDotPotency = new();

        public HashSet<BigInteger> ActiveDots = new();
        public long TotalDotDamage;


        public long Duration()
        {
            return (EndTime - StartTime) switch
            {
                <= 0 => 1, //战斗中
                _ => EndTime - StartTime //战斗结束
            };
        }

        private bool AddPlayer(uint objectId)
        {
            var actor = DalamudApi.ObjectTable.FirstOrDefault(x =>
                x.ObjectId == objectId && x.ObjectKind == ObjectKind.Player);
            if (actor == default || actor.Name.TextValue == "") return false;
            Name.Add(objectId, actor.Name.TextValue);
            DataDic.Add(objectId, new Data());
            DataDic[objectId].Damages = new Dictionary<uint, SkillDamage> {{0, new SkillDamage()}};

            DataDic[objectId].JobId = ((Character) actor).ClassJob.Id;
            DataDic[objectId].PotSkill = Potency.BaseSkill[DataDic[objectId].JobId];
            DataDic[objectId].SkillPotency = 0;
            DataDic[objectId].Speed = 1f;
            return true;
        }

        public void AddSS(uint objectId, float casttime, uint actionId)
        {
            var muti = actionId switch
            {
                7 => 1,
                8 => 1,
                3577 => 2.8f / casttime,
                _ => 1.5f / casttime
            };
            if (DataDic.ContainsKey(objectId) &&
                (DataDic[objectId].Speed > muti || DataDic[objectId].Speed == 1))
                DataDic[objectId].Speed = muti;
        }

        public void AddEvent(int kind, uint @from, uint target, uint id, long damage, byte dc =0)
        {
            if (!DalamudApi.Condition[ConditionFlag.BoundByDuty] &&
                !DalamudApi.Condition[ConditionFlag.InCombat]) return;
            if (from > 0x40000000 && from != 0xE0000000 || from == 0x0)
            {
                PluginLog.Error($"Unknown Id {from:X}");
                return;
            }

            PluginLog.Debug($"AddEvent:{kind}:{from:X}:{target:X}:{id}:{damage}");

            if (!Name.ContainsKey(from) && from != 0xE0000000)
            {
                var added = AddPlayer(from);
                if (!added)
                {
                    PluginLog.Error($"{from:X} is not found");
                    return;
                }
            }
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
                    if (dot.Source > 0x40000000) pet.TryGetValue(dot.Source, out dot.Source);
                    if (dot.Source > 0x40000000) continue;
                    if (!DataDic.ContainsKey(dot.Source)) AddPlayer(dot.Source);
                    if (!DataDic.ContainsKey(dot.Source)) continue;
                    if (PlayerDotPotency.ContainsKey(buff))
                        PlayerDotPotency[buff] += Potency.DotPot[dot.BuffId] * (uint) DataDic[dot.Source].Special;
                    else
                        PlayerDotPotency.Add(buff,
                            Potency.DotPot[dot.BuffId] * (uint) DataDic[dot.Source].Special);
                }
            }

            //伤害
            if (from != 0xE0000000 && kind == 3)
            {
                if (Potency.SkillPot.TryGetValue(id, out var pot)) //基线技能
                {
                    if (DataDic[from].PotSkill == id)
                    {
                        DataDic[from].SkillPotency += pot * Potency.Muti[DataDic[from].JobId];
                    }
                    else if (id > 10)
                    {
                        DataDic[from].PotSkill = id;
                        DataDic[from].SkillPotency = pot * Potency.Muti[DataDic[from].JobId];
                    }
                }

                if (DataDic[from].Damages.ContainsKey(id))
                    DataDic[from].Damages[id].AddDamage(damage);
                else
                {
                    DataDic[from].Damages.Add(id, new SkillDamage(damage));
                    PluginUI.Icon.TryAdd(id,
                        DalamudApi.DataManager.GetImGuiTextureHqIcon(ActionSheet.GetRow(id)!.Icon));
                }
                DataDic[from].Damages[id].AddDC(dc);
                DataDic[from].Damages[0].AddDamage(damage);
                DataDic[from].Damages[0].AddDC(dc);
            }
        }

        private static ulong ActiveToUlong(BigInteger active)
        {
            return (ulong) (active >> 32);
        }

        public static Dot ActiveToDot(BigInteger active)
        {
            return new Dot()
            {
                BuffId = (uint) (active >> 64),
                Source = (uint) ((active >> 32) & 0xFFFFFFFF),
                Target = (uint) (active & 0xFFFFFFFF)
            };
        }

        private static BigInteger DotToActive(Dot dot)
        {
            return ((BigInteger) dot.BuffId << 64) + ((BigInteger) dot.Source << 32) + dot.Target;
        }

        public float PDD(uint actor)
        {
            long result = 1;
            if (DataDic[actor].Damages.TryGetValue(DataDic[actor].PotSkill, out var dmg)) result = dmg.Damage;
            return result * DataDic[actor].Speed / DataDic[actor].SkillPotency;
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
            var npc = (BattleNpc) target;
            foreach (var status in npc.StatusList)
            {
                PluginLog.Debug($"Check Dot on {id:X}:{status.StatusId}:{status.SourceID}");
                if (Potency.DotPot.ContainsKey(status.StatusId))
                {
                    var source = status.SourceID;
                    if (status.SourceID > 0x40000000) pet.TryGetValue(source, out source);
                    ActiveDots.Add(DotToActive(new Dot()
                        {BuffId = status.StatusId, Source = source, Target = id}));
                }
            }

            return true;
        }

        public static void SearchForPet()
        {
            pet.Clear();
            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj == null) continue;
                if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
                var owner = ((BattleNpc)obj).OwnerId;
                if (owner == 0xE0000000) continue;
                if (pet.ContainsKey(owner))
                    pet[owner] = obj.ObjectId;
                else
                    pet.Add(obj.ObjectId, owner);
                PluginLog.Debug($"SearchForPet:{obj.ObjectId:X}:{owner:X}");
            }
        }
    }

}