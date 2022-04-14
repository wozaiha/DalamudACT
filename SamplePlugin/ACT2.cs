using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ACT.Struct;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network;
using Dalamud.Hooking;
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
        public string Name => "ACT Beta";

        public Configuration Configuration;
        private PluginUI PluginUi;
        
        public Dictionary<uint, TextureWrap?> Icon = new();
        public List<ACTBattle> Battles = new(5);
        private ExcelSheet<TerritoryType> terrySheet;


        private delegate void EffectDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<EffectDelegate> EffectEffectHook;

        private delegate void ReceiveAbiltyDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<ReceiveAbiltyDelegate> ReceiveAbilityHook;

        private delegate void ActorControlSelfDelegate(uint entityId, uint id, uint arg0, uint arg1, uint arg2,
            uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
        private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

        private delegate void NpcSpawnDelegate(Int64 a, uint sourceId, IntPtr sourceCharacter);
        private Hook<NpcSpawnDelegate> NpcSpawnHook;

        private delegate void CastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<CastDelegate> CastHook;


        #region OPcode & Hook functions

        private unsafe void Ability(IntPtr ptr, uint sourceId, int length)
        {
            if (sourceId > 0x40000000) ACTBattle.pet.TryGetValue(sourceId, out sourceId);
            if (sourceId is > 0x40000000 or 0x0) return;

            var header = Marshal.PtrToStructure<Header>(ptr);
            var effect = (EffectEntry*)(ptr + sizeof(Header));
            var target = (ulong*)(ptr + sizeof(Header) + 8 * sizeof(EffectEntry) * length);
            PluginLog.Debug($"-----------------------Ability{length}------------------------------");
            for (var i = 0; i < length; i++)
            {
                if (*target == 0x0) break;
                //PluginLog.Debug($"{*target:X}");
                for (var j = 0; j < 8; j++)
                {
                    if (effect->type == 3) //damage
                    {
                        long damage = effect->param0;
                        if (effect->param5 == 0x40) damage += effect->param4 << 16;
                        PluginLog.Debug($"EffectEntry:{3},{sourceId:X}:{(uint)*target}:{header.actionId},{damage}");
                        //if (!Battles[^1].DataDic.ContainsKey(sourceId)) return;
                        Battles[^1].AddEvent(3, sourceId, (uint)*target, header.actionId, damage);
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

        private void StartCast(uint source, IntPtr ptr)
        {
            var data = Marshal.PtrToStructure<ActorCast>(ptr);
            CastHook.Original(source, ptr);
            if (source > 0x40000000) return;
            PluginLog.Debug($"Cast:{source:X}:{data.skillType}:{data.action_id}:{data.cast_time}");
            if (data.skillType == 1 && Potency.SkillPot.ContainsKey(data.action_id))
                if (Battles[^1].DataDic.TryGetValue(source, out _))
                    Battles[^1].AddSS(source, data.cast_time, data.action_id);

            //if (data.action_id == 7489) //彼岸花 回天
            //{
            //    var actor = (PlayerCharacter)DalamudApi.ObjectTable.First(x =>
            //        x.ObjectId == source && x.ObjectKind == ObjectKind.Player);
            //    if (!Battles[^1].DataDic.ContainsKey(source)) return;
            //    Battles[^1].DataDic[source].Special = actor.StatusList.Any(x => x.StatusId == 1229) ? 1.5f : 1.0f;
            //}
        }



        private void ReceiveActorControlSelf(uint entityId, uint type, uint buffID, uint direct, uint damage, uint sourceId,
            uint arg4, uint arg5, ulong targetId, byte a10)
        {
            //PluginLog.Debug($"ReceiveActorControlSelf{entityId:X}:{type}:{buffID}:{direct}:{damage}:{sourceId:X}:");
            ActorControlSelfHook.Original(entityId, type, buffID, direct, damage, sourceId, arg4, arg5, targetId, a10);
            if (entityId < 0x40000000) return;
            if (sourceId > 0x40000000) ACTBattle.pet.TryGetValue(sourceId, out sourceId);
            if (sourceId > 0x40000000) return;
            if (type != 23) return;
            //if (!Battles[^1].DataDic.ContainsKey(sourceId)) return;
            if (buffID != 0)
            {
                if (Potency.BuffToAction.TryGetValue(buffID, out buffID))
                    Battles[^1].AddEvent(3, sourceId, entityId, buffID, damage);
            }
            else
            {
                Battles[^1].AddEvent(3, 0xE000_0000, entityId, 0, damage);
            }
        }

        private unsafe void ReceiveAbilityEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
            IntPtr effectArray, IntPtr effectTrail)
        {
            var targetCount = *(byte*)(effectHeader + 0x21);
            switch (targetCount)
            {
                case 1:
                    Ability(effectHeader, (uint)sourceId, 1);
                    break;
                case <=8 and >1:
                    Ability(effectHeader, (uint)sourceId, 8);
                    break;
                case >8 and <=16:
                    Ability(effectHeader, (uint)sourceId, 16);
                    break;
                case >16 and <=24:
                    Ability(effectHeader, (uint)sourceId, 24);
                    break;
                case >24 and <=32:
                    Ability(effectHeader, (uint)sourceId, 32);
                    break;
            }

            ReceiveAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
        }

        //private void Effect(uint sourceId, IntPtr ptr)
        //{
        //    Ability(ptr, sourceId, 1);
        //    EffectEffectHook.Original(sourceId, ptr);
        //}

        #endregion

        private void CheckTime()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Battles[^1].EndTime > 0 && now - Battles[^1].EndTime > 10)
            {
                Battles[^1].ActiveDots.Clear();
                //新的战斗
                if (Battles.Count == 3) Battles.RemoveAt(0);
                Battles.Add(new ACTBattle(0, 0));
                PluginUi.choosed--;
                if (PluginUi.choosed < 0) PluginUi.choosed = 0;
                ACTBattle.SearchForPet();
            }

            if (Battles[^1].StartTime is 0 && Battles[^1].EndTime is 0)
                if (DalamudApi.ClientState.LocalPlayer != null &&
                    DalamudApi.Condition[ConditionFlag.InCombat])
                {
                    //开始战斗
                    Battles[^1].StartTime = now;
                    Battles[^1].EndTime = now;
                    Battles[^1].Zone = terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceName.Value?.Name ?? "Unknown";
                    PluginUi.choosed = Battles.Count - 1;
                    ACTBattle.SearchForPet();
                }
        }

        private void Update(Framework framework)
        {
            CheckTime();
        }


        public ACT(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);

            terrySheet = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()!;
            ACTBattle.ActionSheet = DalamudApi.DataManager.GetExcelSheet<Action>()!;

            for (uint i = 62100; i < 62141; i++) Icon.Add(i - 62100, DalamudApi.DataManager.GetImGuiTextureHqIcon(i));

            Icon.Add(99, DalamudApi.DataManager.GetImGuiTextureHqIcon(103)); //LB

            Battles.Add(new ACTBattle(0, 0));

            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            #region Hook

            {
                //EffectEffectHook = new Hook<EffectDelegate>(
                //    DalamudApi.SigScanner.ScanText(
                //        "48 89 5C 24 ?? 57 48 83 EC 60 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 48 8B DA"), Effect);
                //EffectEffectHook.Enable();
                ReceiveAbilityHook = new Hook<ReceiveAbiltyDelegate>(
                    DalamudApi.SigScanner.ScanText((int)DalamudApi.ClientState.ClientLanguage > 3 ? "4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9":"4C 89 44 24 ?? 55 56 57 41 54 41 55 41 56 48 8D 6C 24 ??"),
                    ReceiveAbilityEffect);
                ReceiveAbilityHook.Enable();
                ActorControlSelfHook = new Hook<ActorControlSelfDelegate>(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), ReceiveActorControlSelf);
                ActorControlSelfHook.Enable();
                NpcSpawnHook = new Hook<NpcSpawnDelegate>(
                    DalamudApi.SigScanner.ScanText(
                        "E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 83 C4 20 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 "),
                    ReviceNpcSpawn);
                NpcSpawnHook.Enable();
                CastHook = new Hook<CastDelegate>(
                    DalamudApi.SigScanner.ScanText("40 55 56 48 81 EC ?? ?? ?? ?? 48 8B EA"), StartCast);
                CastHook.Enable();
            }

            #endregion

            DalamudApi.Framework.Update += Update;

            PluginUi = new PluginUI(this);
        }

        private void ReviceNpcSpawn(Int64 a, uint target, IntPtr ptr)
        {
            
            var obj = Marshal.PtrToStructure<NpcSpawn>(ptr);
            NpcSpawnHook.Original(a, target, ptr);
            if (obj.spawnerId == 0xE0000000) return;
            if (ACTBattle.pet.ContainsKey(target))
                ACTBattle.pet[target] = obj.spawnerId;
            else
                ACTBattle.pet.Add(target, obj.spawnerId);
            PluginLog.Debug($"Spawn:{target:X}:{obj.spawnerId:X}");
        }

        public void Dispose()
        {
            DalamudApi.Framework.Update -= Update;
            PluginUi?.Dispose();
            foreach (var (id, texture) in Icon) texture?.Dispose();

            ActorControlSelfHook.Disable();
            ReceiveAbilityHook.Disable();
            NpcSpawnHook.Disable();
            CastHook.Disable();

            ActorControlSelfHook.Dispose();
            ReceiveAbilityHook.Dispose();
            NpcSpawnHook.Dispose();
            CastHook.Dispose();
            DalamudApi.Dispose();
        }

        [Command("/act")]
        [HelpMessage("显示Debug窗口.")]
        private void ToggleConfig(string command, string args)
        {
            PluginUi.DrawConfigUI();
        }
    }
}