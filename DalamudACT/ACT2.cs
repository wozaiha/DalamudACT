using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudACT.Struct;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;
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

        //private delegate nint NpcSpawnDelegate(uint sourceId, nint sourceCharacter);

        //private Hook<NpcSpawnDelegate> NpcSpawnHook;

        private delegate void CastDelegate(uint sourceId, nint sourceCharacter);

        private Hook<CastDelegate> CastHook;


        #region OPcode & Hook functions

        private unsafe void Ability(nint headPtr, nint effectPtr, nint targetPtr, uint sourceId, int length)
        {
            DalamudApi.Log.Verbose($"-----------------------Ability{length}:{sourceId:X}------------------------------");
            
            if (sourceId > 0x40000000)
                sourceId = ACTBattle.GetOwner(sourceId);
            
            if (sourceId is > 0x40000000 or 0x0) return;

            var header = Marshal.PtrToStructure<Header>(headPtr);
            var effect = (EffectEntry*) effectPtr;
            var target = (ulong*) targetPtr;

            for (var i = 0; i < length; i++)
            {
                DalamudApi.Log.Verbose(
                    $"{*target:X} effect:{effect->type}:{effect->param0}:{effect->param1}:{effect->param2}:{effect->param3}:{effect->param4}:{effect->param5}");
                if (*target == 0x0) break;
                //DalamudApi.Log.Verbose($"{*target:X}");
                for (var j = 0; j < 8; j++)
                {
                    if (effect->type == 3) //damage
                    {
                        long damage = effect->param0;
                        if (effect->param5 == 0x40) damage += effect->param4 << 16;
                        DalamudApi.Log.Verbose($"EffectEntry:{3},{sourceId:X}:{(uint) *target:X}:{header.actionId},{damage}");
                        Battles[^1].AddEvent(EventKind.Damage, sourceId, (uint) *target, header.actionId, damage,
                            effect->param1);
                    }

                    effect++;
                }

                target++;
            }

            DalamudApi.Log.Verbose("------------------------END------------------------------");
        }

        private void StartCast(uint source, nint ptr)
        {
            var data = Marshal.PtrToStructure<ActorCast>(ptr);
            CastHook.Original(source, ptr);
            if (source > 0x40000000) return;
            DalamudApi.Log.Verbose(
                $"Cast:{source:X}:{data.skillType}:{data.action_id}:{data.cast_time}:{data.flag}:{data.unknown_2:X}:{data.unknown_3}");
            if (data.skillType == 1 && Potency.SkillPot.ContainsKey(data.action_id))
                if (Battles[^1].DataDic.TryGetValue(source, out _))
                    Battles[^1].AddSS(source, data.cast_time, data.action_id);
        }

        private void ReceiveActorControlSelf(uint entityId, ActorControlCategory type, uint arg0, uint arg1, uint arg2,
            uint arg3,
            uint arg4, uint arg5, ulong targetId, byte a10)
        {
            //DalamudApi.Log.Verbose($"ReceiveActorControlSelf{entityId:X}:{type}:0={arg0}:1={arg1}:2={arg2}:3={arg3}:4={arg4}:5={arg5}:{targetId:X}:{a10}");
            ActorControlSelfHook.Original(entityId, type, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (type == ActorControlCategory.Death && entityId < 0x40000000)
            {
                Battles[^1].AddEvent(EventKind.Death, entityId, arg0, 0, 0);
                DalamudApi.Log.Verbose($"{entityId:X} killed by {arg0:X}");
                return;
            }

            // actorid:death:id1:id2:?:?:?:?:E0000000:0
            if (entityId < 0x40000000) return;
            if (arg2 > 0x40000000) arg2 = ACTBattle.GetOwner(arg2);
            if (arg2 > 0x40000000) return;

            if (type is ActorControlCategory.DoT)
            {
                DalamudApi.Log.Verbose($"Dot:{arg0} from {arg2:X} ticked {arg1} damage on {entityId:X}");
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

        //private nint ReceiveNpcSpawn(uint target, nint ptr)
        //{
        //    var obj = Marshal.PtrToStructure<NpcSpawn>(ptr);
        //    var result = NpcSpawnHook.Original(target, ptr);
        //    DalamudApi.Log.Verbose($"Spawn:{target:X}:{obj.spawnerId:X}");
        //    if (obj.spawnerId == 0xE0000000) return result;
        //    ACTBattle.Pet[target] = obj.spawnerId;
        //    return result;
        //}

        #endregion

        private void CheckTime()
        {
            var inCombat = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
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
                //ACTBattle.SearchForPet();
            }

            if (DalamudApi.ClientState.LocalPlayer != null && inCombat)
            {
                //开始战斗
                if (Battles[^1].StartTime is 0) Battles[^1].StartTime = now;
                Battles[^1].EndTime = now;
                Battles[^1].Zone = GetPlaceName();


                PluginUI.choosed = Battles.Count - 1;
            }
        }

        private string GetPlaceName()
        {
            var result = "Unknown";
            var excel = terrySheet.GetRow(DalamudApi.ClientState.TerritoryType);
            if (excel.ContentFinderCondition.ValueNullable.HasValue)
            {
                return excel.ContentFinderCondition.Value.Name.ExtractText();
            }
            else
            {
                if (excel.PlaceName.ValueNullable.HasValue)
                {
                    return excel.PlaceName.Value.Name.ExtractText();
                }
                else if (excel.PlaceNameRegion.ValueNullable.HasValue)
                {
                    return excel.PlaceNameRegion.Value.Name.ExtractText();
                }
                else if (excel.PlaceNameZone.ValueNullable.HasValue)
                {
                    return excel.PlaceNameZone.Value.Name.ExtractText();
                }
            }
            return result;
        }

        private void Update(IFramework framework)
        {
            CheckTime();
        }


        public ACT(IDalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(pluginInterface);
            terrySheet = DalamudApi.GameData.GetExcelSheet<TerritoryType>()!;
            ACTBattle.ActionSheet = DalamudApi.GameData.GetExcelSheet<Action>()!;

            for (uint i = 62100; i <= 62100 + 42; i++) Icon.Add(i - 62100, DalamudApi.Textures.GetFromGameIcon(new GameIconLookup(i)).RentAsync().Result);

            Icon.Add(99, DalamudApi.Textures.GetFromGameIcon(new GameIconLookup(103)).RentAsync().Result); //LB

            Battles.Add(new ACTBattle(0, 0));

            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            #region Hook
            {
                ReceiveAbilityHook = DalamudApi.Interop.HookFromAddress<ReceiveAbilityDelegate>(
                    DalamudApi.SigScanner.ScanText("40 55 53 56 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70"),
                    ReceiveAbilityEffect);
                ReceiveAbilityHook.Enable();
                //48 89 5C 24 ? 57 48 83 EC 60 48 8B 05 ? ? ? ? 48 33 C4 48 89 44 24 ? 48 8B DA 最后函数

                ActorControlSelfHook = DalamudApi.Interop.HookFromAddress<ActorControlSelfDelegate>(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64 "), ReceiveActorControlSelf);
                ActorControlSelfHook.Enable();

                //var SpawnSig = "E8 ?? ?? ?? ?? B0 01 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 50 5F C3 8B 4F 08 48 8B D3 E8 ?? ?? ?? ?? B0 01 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 50 5F C3 8B 4F 08 48 8B D3 E8 ?? ?? ?? ?? B0 01 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 50 5F C3 8B 4F 08 48 8B D3 E8 ?? ?? ?? ?? B0 01 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 50 5F C3 8B 4F 08 48 8B D3 E8 ?? ?? ?? ?? B0 01 48 8B 5C 24 ?? 48 8B 74 24 ?? 48 83 C4 50 5F C3 0F B7 13";
                //NpcSpawnHook = DalamudApi.Interop.HookFromAddress<NpcSpawnDelegate>(
                //    DalamudApi.SigScanner.ScanText(SpawnSig),
                //    ReceiveNpcSpawn);
                //NpcSpawnHook.Enable();

                CastHook = DalamudApi.Interop.HookFromAddress<CastDelegate>(
                    DalamudApi.SigScanner.ScanText("40 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B F2 "), StartCast);
                CastHook.Enable();
                //E8 ? ? ? ? 84 C0 75 12 0F 57 C0 第一个
            }

            #endregion

            DalamudApi.Framework.Update += Update;

            PluginUi = new PluginUI(this);

            DalamudApi.Commands.AddHandler("/act", new CommandInfo(OnCommand)
            {
                HelpMessage = "/act 显示DAct主窗口\n /act config 显示设置窗口"
            });

            DalamudApi.PluginInterface.UiBuilder.Draw += DrawUI;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            DalamudApi.PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;

            //DalamudApi.Log.Warning($"-{DalamudApi.GameData.Excel.GetSheet<World>()!.GetRow(1081)!.IsPublic}");
        }
        
        public void Disable()
        {
            ActorControlSelfHook.Disable();
            ReceiveAbilityHook.Disable();
            //NpcSpawnHook.Disable();
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
            //NpcSpawnHook.Dispose();
            CastHook.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            switch (args)
            {
                case null or "":
                    PluginUi.mainWindow.IsOpen = true; 
                    break;
                case "config":
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

        public void DrawMainUI()
        {
            PluginUi.mainWindow.IsOpen = true;
        }
    }
}