using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace DalamudACT.Struct;

public class ACTBattle
{
    private const int Total = 0;


    public static ExcelSheet<Action>? ActionSheet;
    public static readonly Dictionary<uint, uint> Pet = new();
    public readonly Dictionary<uint, long> LimitBreak = new();

    public long StartTime;
    public long EndTime;
    public string? Zone;
    public int? Level;
    public readonly Dictionary<uint, string> Name = new();
    public readonly Dictionary<uint, Data> DataDic = new();
    public readonly Dictionary<long, long> PlayerDotPotency = new();

    public readonly List<Dot> ActiveDots = new();
    public long TotalDotDamage;
    public float TotalDotSim;
    public Dictionary<long, float> DotDmgList = new();


    public ACTBattle(long time1, long time2)
    {
        StartTime = time1;
        EndTime = time2;
        Level = DalamudApi.ClientState.LocalPlayer?.Level;
    }

    public class Dot
    {
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
        public uint Death = 0;
    }

    public class SkillDamage
    {
        public long Damage = 0;
        public uint D = 0;
        public uint C = 0;
        public uint DC = 0;
        public uint swings = 0;

        public SkillDamage(long damage = 0)
        {
            Damage = damage;
        }

        public void AddDC(byte dc)
        {
            switch (dc)
            {
				case 64:
					D++;
					break;
				case 32:
					C++;
					break;
				case 96:
					DC++;
					D++;
					C++;
					break;
			}

            swings++;
        }

        public void AddDamage(long damage)
        {
            Damage += damage;
        }
    }
    


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

    public enum EventKind
    {
        Damage = 3,
        Death = 6,
    }

    public void AddEvent(EventKind eventKind, uint from, uint target, uint id, long damage, byte dc = 0)
    {
        if (!DalamudApi.Condition[ConditionFlag.BoundByDuty] &&
            !DalamudApi.Condition[ConditionFlag.InCombat]) return;

        if (from > 0x40000000 && from != 0xE0000000 || from == 0x0)
        {
            PluginLog.Error($"Unknown Id {from:X}");
            return;
        }

        PluginLog.Debug($"AddEvent:{eventKind}:{from:X}:{target:X}:{id}:{damage}");

        if (!Name.ContainsKey(from) && from != 0xE0000000)
        {
            var added = AddPlayer(from);
            if (!added)
            {
                PluginLog.Error($"{from:X} is not found");
                return;
            }
        }

        //死亡
        if (eventKind == EventKind.Death)
        {
            if (!Name.ContainsKey(from)) return;
            DataDic[from].Death++;
            return;
        }

        //DOT 伤害
        if (from == 0xE0000000 && eventKind == EventKind.Damage)
        {
            if (!CheckTargetDot(target)) return;
            TotalDotDamage += damage;
            foreach (var dot in ActiveDots)
            {
                if (dot.Source > 0x40000000) Pet.TryGetValue(dot.Source, out dot.Source);
                if (dot.Source > 0x40000000) continue;
                if (!DataDic.ContainsKey(dot.Source)) AddPlayer(dot.Source);
                if (!DataDic.ContainsKey(dot.Source)) continue;
                
                var active = DotToActive(dot);
                if (PlayerDotPotency.ContainsKey(active))
                    PlayerDotPotency[active] += Potency.DotPot[dot.BuffId];
                else
                    PlayerDotPotency.Add(active,Potency.DotPot[dot.BuffId]);
            }

            CalcDot();
        }

        //伤害
        if (from != 0xE0000000 && eventKind == EventKind.Damage)
        {

            if (ActionSheet.GetRow(id)?.PrimaryCostType == 11) //LimitBreak
            {
                if (LimitBreak.ContainsKey(id)) LimitBreak[id] += damage;
                else LimitBreak.Add(id,damage);
            }
            else 
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
                {
                    DataDic[from].Damages[id].AddDamage(damage);
                }
                else
                {
                    DataDic[from].Damages.Add(id, new SkillDamage(damage));
                    PluginUI.Icon.TryAdd(id,
                        DalamudApi.DataManager.GetImGuiTextureHqIcon(ActionSheet.GetRow(id)!.Icon));
                }

                DataDic[from].Damages[id].AddDC(dc);
                DataDic[from].Damages[Total].AddDamage(damage);
                DataDic[from].Damages[Total].AddDC(dc);
            }
        }
    }

    private void CalcDot()
    {
        TotalDotSim = 0;
        foreach (var (active, potency) in PlayerDotPotency)
        {
            var source = (uint) (active & 0xFFFFFFFF);
            var dmg = DPP(source) * potency;
            TotalDotSim += dmg;
            if (!DotDmgList.TryAdd(active,dmg)) DotDmgList[active] = dmg;
        }

        foreach (var (active,damage) in DotDmgList)
        {
            DotDmgList[active]  = damage / TotalDotSim * TotalDotDamage;
        }

        var dic = from entry in DotDmgList orderby entry.Value descending select entry;
        DotDmgList = dic.ToDictionary(x=> x.Key,x => x.Value);
    }

    private static long DotToActive(Dot dot)
    {
        return ((long) dot.BuffId << 32) + dot.Source;
    }

    public float DPP(uint actor)
    {
        long result = 1;
        if (DataDic[actor].Damages.TryGetValue(DataDic[actor].PotSkill, out var dmg)) result = dmg.Damage;
        return result * DataDic[actor].Speed / DataDic[actor].SkillPotency;
    }

    private bool CheckTargetDot(uint id)
    {
        ActiveDots.Clear();
        var target = DalamudApi.ObjectTable.SearchById(id);
        if (target == null || target.ObjectKind != ObjectKind.BattleNpc)
        {
            PluginLog.Error($"Dot target {id:X} is not BattleNpc");
            return false;
        }

        var npc = (BattleNpc) target;
        foreach (var status in npc.StatusList)
        {
            PluginLog.Debug($"Check Dot on {id:X}:{status.StatusId}:{status.SourceId}");
            if (Potency.DotPot.ContainsKey(status.StatusId))
            {
                var source = status.SourceId;
                if (status.SourceId > 0x40000000) Pet.TryGetValue(source, out source);
                ActiveDots.Add(new Dot()
                    {BuffId = status.StatusId, Source = source});
            }
        }

        return true;
    }

    public static void SearchForPet()
    {
        Pet.Clear();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null) continue;
            if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
            var owner = ((BattleNpc) obj).OwnerId;
            if (owner == 0xE0000000) continue;
            if (Pet.ContainsKey(owner))
                Pet[owner] = obj.ObjectId;
            else
                Pet.Add(obj.ObjectId, owner);
            PluginLog.Debug($"SearchForPet:{obj.ObjectId:X}:{owner:X}");
        }
    }
}