using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudACT.Struct;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;
using EventKind = DalamudACT.Struct.ACTBattle.EventKind;

namespace DalamudACT
{
    public class ACT : IDalamudPlugin
    {
        public string Name => "Dalamud Damage Display";

        public Configuration Configuration;
        private PluginUI PluginUi;

        public Dictionary<uint, IDalamudTextureWrap?> Icon = new();
        public List<ACTBattle> Battles = new(5);
        private ExcelSheet<TerritoryType> terrySheet;
        
        private delegate void ReceiveAbilityDelegate(int sourceId, nint sourceCharacter, nint pos,
            nint effectHeader, nint effectArray, nint effectTrail);

        private Hook<ReceiveAbilityDelegate> ReceiveAbilityHook;

        private delegate void ActorControlSelfDelegate(uint entityId, ActorControlCategory id, uint arg0, uint arg1,
            uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);

        private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

        private delegate void NpcSpawnDelegate(nint a, uint sourceId, nint sourceCharacter);

        private Hook<NpcSpawnDelegate> NpcSpawnHook;

        private delegate void CastDelegate(uint sourceId, nint sourceCharacter);

        private Hook<CastDelegate> CastHook;


        #region OPcode & Hook functions

        private unsafe void Ability(nint headPtr, nint effectPtr, nint targetPtr, uint sourceId, int length)
        {
            DalamudApi.Log.Debug($"-----------------------Ability{length}:{sourceId:X}------------------------------");
            if (sourceId > 0x40000000) ACTBattle.Pet.TryGetValue(sourceId, out sourceId);
            if (sourceId is > 0x40000000 or 0x0) return;

            var header = Marshal.PtrToStructure<Header>(headPtr);
            var effect = (EffectEntry*) effectPtr;
            var target = (ulong*) targetPtr;

            for (var i = 0; i < length; i++)
            {
                DalamudApi.Log.Debug(
                    $"{*target:X} effect:{effect->type}:{effect->param0}:{effect->param1}:{effect->param2}:{effect->param3}:{effect->param4}:{effect->param5}");
                if (*target == 0x0) break;
                //DalamudApi.Log.Debug($"{*target:X}");
                for (var j = 0; j < 8; j++)
                {
                    if (effect->type == 3) //damage
                    {
                        long damage = effect->param0;
                        if (effect->param5 == 0x40) damage += effect->param4 << 16;
                        DalamudApi.Log.Debug($"EffectEntry:{3},{sourceId:X}:{(uint) *target:X}:{header.actionId},{damage}");
                        Battles[^1].AddEvent(EventKind.Damage, sourceId, (uint) *target, header.actionId, damage,
                            effect->param1);
                    }

                    effect++;
                }

                target++;
            }

            DalamudApi.Log.Debug("------------------------END------------------------------");
        }

        private void StartCast(uint source, nint ptr)
        {
            var data = Marshal.PtrToStructure<ActorCast>(ptr);
            CastHook.Original(source, ptr);
            if (source > 0x40000000) return;
            DalamudApi.Log.Debug(
                $"Cast:{source:X}:{data.skillType}:{data.action_id}:{data.cast_time}:{data.flag}:{data.unknown_2:X}:{data.unknown_3}");
            if (data.skillType == 1 && Potency.SkillPot.ContainsKey(data.action_id))
                if (Battles[^1].DataDic.TryGetValue(source, out _))
                    Battles[^1].AddSS(source, data.cast_time, data.action_id);
        }

        private void ReceiveActorControlSelf(uint entityId, ActorControlCategory type, uint arg0, uint arg1, uint arg2,
            uint arg3,
            uint arg4, uint arg5, ulong targetId, byte a10)
        {
            //DalamudApi.Log.Debug($"ReceiveActorControlSelf{entityId:X}:{type}:0={arg0}:1={arg1}:2={arg2}:3={arg3}:4={arg4}:5={arg5}:{targetId:X}:{a10}");
            ActorControlSelfHook.Original(entityId, type, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (type == ActorControlCategory.Death && entityId < 0x40000000)
            {
                Battles[^1].AddEvent(EventKind.Death, entityId, arg0, 0, 0);
                DalamudApi.Log.Debug($"{entityId:X} killed by {arg0:X}");
                return;
            }

            // actorid:death:id1:id2:?:?:?:?:E0000000:0
            if (entityId < 0x40000000) return;
            if (arg2 > 0x40000000) ACTBattle.Pet.TryGetValue(arg2, out arg2);
            if (arg2 > 0x40000000) return;

            if (type is ActorControlCategory.DoT)
            {
                DalamudApi.Log.Debug($"Dot:{arg0} from {arg2:X} ticked {arg1} damage on {entityId:X}");
                if (arg0 != 0 && Potency.BuffToAction.TryGetValue(arg0, out arg0))
                {
                    Battles[^1].AddEvent(EventKind.Damage, arg2, entityId, arg0, arg1);
                }
                else
                    Battles[^1].AddEvent(EventKind.Damage, 0xE000_0000, entityId, 0, arg1);
            }
        }

        private unsafe void ReceiveAbilityEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
            IntPtr effectArray, IntPtr effectTrail)
        {
            var targetCount = *(byte*) (effectHeader + 0x21);
            switch (targetCount)
            {
                case <= 1:
                    Ability(effectHeader, effectArray, effectTrail, (uint) sourceId, 1);
                    break;
                case <= 8 and > 1:
                    Ability(effectHeader, effectArray, effectTrail, (uint) sourceId, 8);
                    break;
                case > 8 and <= 16:
                    Ability(effectHeader, effectArray, effectTrail, (uint) sourceId, 16);
                    break;
                case > 16 and <= 24:
                    Ability(effectHeader, effectArray, effectTrail, (uint) sourceId, 24);
                    break;
                case > 24 and <= 32:
                    Ability(effectHeader, effectArray, effectTrail, (uint) sourceId, 32);
                    break;
            }

            ReceiveAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
        }
        
        #endregion

        private void CheckTime()
        {
            var inCombat = (DalamudApi.ClientState.LocalPlayer?.StatusFlags & StatusFlags.InCombat) != 0;
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Battles[^1].EndTime > 0 && !inCombat)
            {
                Battles[^1].ActiveDots.Clear();
                //新的战斗
                if (Battles.Count == 5)
                {
                    Battles.RemoveAt(0);
                    PluginUI.choosed--;
                }

                Battles.Add(new ACTBattle(0, 0));
                ACTBattle.SearchForPet();
            }

            if (DalamudApi.ClientState.LocalPlayer != null && inCombat)
            {
                //开始战斗
                if (Battles[^1].StartTime is 0) Battles[^1].StartTime = now;
                Battles[^1].EndTime = now;
                Battles[^1].Zone = !string.IsNullOrEmpty(terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)
                    ?.ContentFinderCondition.Value?.Name!)
                    ? terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.ContentFinderCondition.Value?.Name!
                    : terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceName.Value?.Name
                      ?? terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceNameRegion.Value?.Name
                      ?? terrySheet.GetRow(DalamudApi.ClientState.TerritoryType)?.PlaceNameZone.Value?.Name
                      ?? "Unknown";

                PluginUI.choosed = Battles.Count - 1;
            }
        }

        private void Update(IFramework framework)
        {
            CheckTime();
        }


        public ACT(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(pluginInterface);
            terrySheet = DalamudApi.GameData.GetExcelSheet<TerritoryType>()!;
            ACTBattle.ActionSheet = DalamudApi.GameData.GetExcelSheet<Action>()!;

            for (uint i = 62100; i < 62141; i++) Icon.Add(i - 62100, DalamudApi.Textures.GetIcon(i));

            Icon.Add(99, DalamudApi.Textures.GetIcon(103)); //LB

            Battles.Add(new ACTBattle(0, 0));

            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            #region Hook
            {
                ReceiveAbilityHook = DalamudApi.Interop.HookFromAddress<ReceiveAbilityDelegate>(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 4C 24 ?? 48 33 CC E8 ?? ?? ?? ?? 48 8B 9C 24 ?? ?? ?? ?? 48 83 C4 60 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 ??"),
                    ReceiveAbilityEffect);
                ReceiveAbilityHook.Enable();
                ActorControlSelfHook = DalamudApi.Interop.HookFromAddress<ActorControlSelfDelegate>(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), ReceiveActorControlSelf);
                ActorControlSelfHook.Enable();

                var SpawnSig = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 8B F2 49 8B D8 41 0F B6 50 ?? 48 8B F9 E8 ?? ?? ?? ?? 4C 8D 44 24 ?? C7 44 24 ?? ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 66 0F 1F 84 00 ?? ?? ?? ?? 4D 8D 80 ?? ?? ?? ?? 0F 10 03 0F 10 4B 10 48 8D 9B ?? ?? ?? ?? 41 0F 11 40 ?? 0F 10 43 A0 41 0F 11 48 ?? 0F 10 4B B0 41 0F 11 40 ?? 0F 10 43 C0 41 0F 11 48 ?? 0F 10 4B D0 41 0F 11 40 ?? 0F 10 43 E0 41 0F 11 48 ?? 0F 10 4B F0 41 0F 11 40 ?? 41 0F 11 48 ?? 48 83 E8 01 75 A5 48 8B 03 ";
                NpcSpawnHook = DalamudApi.Interop.HookFromAddress<NpcSpawnDelegate>(
                    DalamudApi.SigScanner.ScanText(SpawnSig),
                    ReceiveNpcSpawn);
                NpcSpawnHook.Enable();
                CastHook = DalamudApi.Interop.HookFromAddress<CastDelegate>(
                    DalamudApi.SigScanner.ScanText("40 55 56 48 81 EC ?? ?? ?? ?? 48 8B EA"), StartCast);
                CastHook.Enable();
            }

            #endregion

            DalamudApi.Framework.Update += Update;
            
            PluginUi = new PluginUI(this);

            DalamudApi.Commands.AddHandler("/act", new CommandInfo(OnCommand)
            {
                HelpMessage = "显示DAct设置窗口."
            });

            DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

        }

        private void ReceiveNpcSpawn(nint a, uint target, nint ptr)
        {
            var obj = Marshal.PtrToStructure<NpcSpawn>(ptr);
            NpcSpawnHook.Original(a, target, ptr);
            if (obj.spawnerId == 0xE0000000) return;
            ACTBattle.Pet[target] = obj.spawnerId;
            DalamudApi.Log.Debug($"Spawn:{target:X}:{obj.spawnerId:X}");
        }

        public void Disable()
        {
            ActorControlSelfHook.Disable();
            ReceiveAbilityHook.Disable();
            NpcSpawnHook.Disable();
            CastHook.Disable();
            //EffectHook.Disable();
        }

        public void Dispose()
        {
            DalamudApi.Framework.Update -= Update;
            PluginUi?.Dispose();
            foreach (var (id, texture) in Icon) texture?.Dispose();
            Disable();
            ActorControlSelfHook.Dispose();
            ReceiveAbilityHook.Dispose();
            NpcSpawnHook.Dispose();
            CastHook.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            switch (args)
            {
                case "" or null:
                    PluginUi.configWindow.IsOpen = true;
                    break;
                case "debug":
                    PluginUi.debugWindow.IsOpen = true;
                    break;
            }
        }

        private void DrawUI()
        {
            PluginUi.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            PluginUi.configWindow.IsOpen = true;
        }
    }
}