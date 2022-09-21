using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using DalamudACT.Struct;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DalamudACT
{
    public class ACT : IDalamudPlugin
    {
        public string Name => "Dalamud Damage Display";
        
        public Configuration Configuration;
        private PluginUI PluginUi;
        
        public Dictionary<uint, TextureWrap?> Icon = new();
        public List<ACTBattle> Battles = new(5);
        private ExcelSheet<TerritoryType> terrySheet;


        private delegate void EffectDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<EffectDelegate> EffectHook;

        private delegate void ReceiveAbilityDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<ReceiveAbilityDelegate> ReceiveAbilityHook;

        private delegate void ActorControlSelfDelegate(uint entityId, ActorControlCategory id, uint arg0, uint arg1, uint arg2,
            uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
        private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

        private delegate void NpcSpawnDelegate(long a, uint sourceId, IntPtr sourceCharacter);
        private Hook<NpcSpawnDelegate> NpcSpawnHook;

        private delegate void CastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<CastDelegate> CastHook;


        #region OPcode & Hook functions

        private unsafe void Ability(IntPtr headPtr, IntPtr effectPtr, uint sourceId, int length)
        {
            PluginLog.Debug($"-----------------------Ability{length}:{sourceId:X}------------------------------");
            if (sourceId > 0x40000000) ACTBattle.pet.TryGetValue(sourceId, out sourceId);
            if (sourceId is > 0x40000000 or 0x0) return;

            var header = Marshal.PtrToStructure<Header>(headPtr);
            var effect = (EffectEntry*)effectPtr;
            var target = (ulong*)(effectPtr + 8 * sizeof(EffectEntry) * length + sizeof(Ender));
            
            for (var i = 0; i < length; i++)
            {
                PluginLog.Debug($"{*target:X} effect:{effect->type}:{effect->param0}:{effect->param1}:{effect->param2}:{effect->param3}:{effect->param4}:{effect->param5}");
                if (*target == 0x0) break; 
                //PluginLog.Debug($"{*target:X}");
                for (var j = 0; j < 8; j++)
                {
                    if (effect->type == 3) //damage
                    {
                        long damage = effect->param0;
                        if (effect->param5 == 0x40) damage += effect->param4 << 16;
                        PluginLog.Debug($"EffectEntry:{3},{sourceId:X}:{(uint)*target}:{header.actionId},{damage}");
                        if (j is 0 && header.actionId == 25750) //英勇之剑
                            Battles[^1].AddEvent(3, sourceId, (uint)*target, header.actionId, damage, effect->param1,true);
                        else Battles[^1].AddEvent(3, sourceId, (uint)*target, header.actionId, damage, effect->param1);
                    }
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
        }



        private void ReceiveActorControlSelf(uint entityId, ActorControlCategory type, uint buffID, uint direct, uint damage, uint sourceId,
            uint arg4, uint arg5, ulong targetId, byte a10)
        {
            //PluginLog.Debug($"ReceiveActorControlSelf{entityId:X}:{type}:{buffID}:{direct}:{damage}:{sourceId:X}:");
            ActorControlSelfHook.Original(entityId, type, buffID, direct, damage, sourceId, arg4, arg5, targetId, a10);
            if (type == ActorControlCategory.Death && entityId < 0x40000000)
            {
                Battles[^1].AddEvent(6, entityId, buffID, 0, 0);
                PluginLog.Error($"{entityId:X} killed by {buffID:X}");
                return;
            } 
            // actorid:death:id1:id2:?:?:?:?:E0000000:0
            if (entityId < 0x40000000) return;
            if (sourceId > 0x40000000) ACTBattle.pet.TryGetValue(sourceId, out sourceId);
            if (sourceId > 0x40000000) return;
            
                
            if (type != ActorControlCategory.HoT_DoT) return;
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
                    Ability(effectHeader,effectArray, (uint)sourceId, 1);
                    break;
                case <= 8 and >1:
                    Ability(effectHeader,effectArray, (uint)sourceId, 8);
                    break;
                case >8 and <=16:
                    Ability(effectHeader,effectArray, (uint)sourceId, 16);
                    break;
                case >16 and <=24:
                    Ability(effectHeader,effectArray, (uint)sourceId, 24);
                    break;
                case >24 and <=32:
                    Ability(effectHeader,effectArray, (uint)sourceId, 32);
                    break;
            }

            ReceiveAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
        }

        //private void Effect(uint sourceId, IntPtr ptr)
        //{
        //    Ability(ptr, sourceId, 1);
        //    EffectHook.Original(sourceId, ptr);
        //}

        #endregion

        private void CheckTime()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Battles[^1].EndTime > 0 && now - Battles[^1].EndTime > 10)
            {
                Battles[^1].ActiveDots.Clear();
                //新的战斗
                if (Battles.Count == 5)
                {
                    Battles.RemoveAt(0);
                    PluginUi.choosed--;
                }
                Battles.Add(new ACTBattle(0, 0));
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
                    //ACTBattle.SearchForPet();
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
                //EffectHook = new Hook<EffectDelegate>(
                //    DalamudApi.SigScanner.ScanText(
                //        "48 89 5C 24 ?? 57 48 83 EC 60 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 48 8B DA"), Effect);
                //EffectHook.Enable();
                ReceiveAbilityHook = Hook<ReceiveAbilityDelegate>.FromAddress(
				   DalamudApi.SigScanner.ScanText("4C 89 44 24 ?? 55 56 57 41 54 41 55 41 56 48 8D 6C 24 ??"),
                    ReceiveAbilityEffect);
                ReceiveAbilityHook.Enable(); 
                ActorControlSelfHook =Hook<ActorControlSelfDelegate>.FromAddress(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), ReceiveActorControlSelf);
                ActorControlSelfHook.Enable();
                NpcSpawnHook = Hook<NpcSpawnDelegate>.FromAddress(
                    DalamudApi.SigScanner.ScanText(
                        "E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 83 C4 20 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 8B F9 "),
                    ReviceNpcSpawn);
                NpcSpawnHook.Enable();
                CastHook =Hook<CastDelegate>.FromAddress(
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
            //EffectHook.Dispose();
            DalamudApi.Dispose();
        }

        [Command("/act")]
        [HelpMessage("显示设置窗口.")]
        private void ToggleConfig(string command, string args)
        {
            switch (args)
            {
                case "" or null:
                    PluginUi.DrawConfigUI();
                    break;
                case "debug":
                    PluginUi.ShowDebug();
                    break;
            }
        }
    }
}