using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Internal;
using ImGuiNET;
using Lumina.Excel;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Dalamud.Interface.Windowing;
using DalamudACT.Struct;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace DalamudACT;

internal class PluginUI : IDisposable
{
    private static Configuration config;

    private static ACT _plugin;
    public static int choosed;
    private static ExcelSheet<Action> sheet = DalamudApi.GameData.GetExcelSheet<Action>()!;
    public static Dictionary<uint, IDalamudTextureWrap?> Icon = new();
    private static ExcelSheet<Status> buffSheet = DalamudApi.GameData.GetExcelSheet<Status>()!;
    public static Dictionary<uint, IDalamudTextureWrap?> BuffIcon = new();
    private static Dictionary<uint, float> DotDictionary;
    private static IDalamudTextureWrap? mainIcon;

    public ConfigWindow configWindow;
    public DebugWindow debugWindow;
    public MainWindow mainWindow;
    public WindowSystem WindowSystem = new("ACT");

    public PluginUI(ACT p)
    {
        _plugin = p;
        config = p.Configuration;

        mainIcon = File.Exists(DalamudApi.PluginInterface.AssemblyLocation.Directory?.FullName + "\\DDD.png")
            ? DalamudApi.PluginInterface?.UiBuilder.LoadImage(
                DalamudApi.PluginInterface.AssemblyLocation.Directory?.FullName + "\\DDD.png")
            : DalamudApi.Textures.GetIcon(62142);

        configWindow = new ConfigWindow(_plugin);
        debugWindow = new DebugWindow(_plugin);
        mainWindow = new MainWindow(_plugin);

        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(debugWindow);
        WindowSystem.AddWindow(mainWindow);

        mainWindow.IsOpen = true;

    }

    public void Dispose()
    {
        foreach (var (_, texture) in Icon) texture?.Dispose();

        foreach (var (_, texture) in BuffIcon) texture?.Dispose();
        mainIcon?.Dispose();

        WindowSystem.RemoveAllWindows();
        configWindow.Dispose();
        debugWindow.Dispose();
        mainWindow?.Dispose();
    }
    
    public class ConfigWindow : Window, IDisposable
    {

        public ConfigWindow(ACT plugin) : base("ACT Config Window", ImGuiWindowFlags.AlwaysAutoResize, false)
        {

        }

        public override void Draw()
        {
            var changed = false;
            changed |= ImGui.Checkbox("Lock MainWindow Position", ref config.Lock);
            changed |= ImGui.Checkbox("No Resize", ref config.NoResize);
            changed |= ImGui.DragInt("BackGround Alpha", ref config.BGColor, 1, 1, 1);
            changed |= ImGui.Checkbox("Show Delta", ref config.delta);

            ImGui.Separator();

            changed |= ImGui.Checkbox("存储最近战斗数据", ref config.SaveData);
            if (config.SaveData)
            {
                changed |= ImGui.InputInt("储存时长", ref config.SaveTime, 1);
                if (config.SaveTime < 0) config.SaveTime = 0;
                if (config.SaveTime > 120) config.SaveTime = 120;
                changed |= ImGui.InputInt("计算时长", ref config.CalcTime, 1);
                if (config.CalcTime < 0) config.CalcTime = 0;
                if (config.CalcTime > config.SaveTime) config.CalcTime = config.SaveTime;
            }

            if (changed) config.Save();

        }

        public void Dispose()
        {
            
        }
    }

    public class DebugWindow : Window, IDisposable
    {

        public DebugWindow(ACT plugin) : base("ACT Debug Window")
        {

        }

        public override void Draw()
        {
            ImGui.Text(
                $"Total Dot DPS:{_plugin.Battles[choosed].TotalDotDamage / _plugin.Battles[choosed].Duration()}");

            if (ImGui.BeginTable("Pot", 6))
            {
                var headers = new string[]
                    {"Name", "ActorId", "PotSkill", "SkillPotency", "Speed", "DPP"};
                foreach (var t in headers) ImGui.TableSetupColumn(t);
                ImGui.TableHeadersRow();

                foreach (var (actor, damage) in _plugin.Battles[choosed].DataDic)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_plugin.Battles[choosed].Name[actor]}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{actor:X}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{sheet.GetRow(damage.PotSkill)!.Name}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{damage.SkillPotency}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{damage.Speed}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_plugin.Battles[choosed].DPP(actor)}");
                }
            }

            ImGui.EndTable();
            ImGui.Separator();

            ImGui.BeginTable("Dots", 5);
            {
                var headers = new string[] { "BuffId", "Source", "Potency", "Simulated DPS", "Split DPS" };

                foreach (var t in headers) ImGui.TableSetupColumn(t);

                ImGui.TableHeadersRow();
                var total = 0f;
                foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
                {
                    var source = (uint)(active & 0xFFFFFFFF);
                    total += _plugin.Battles[choosed].DPP(source) * potency;
                }

                foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
                {
                    ImGui.TableNextColumn();
                    var buff = (uint)(active >> 32);
                    var source = (uint)(active & 0xFFFFFFFF);
                    ImGui.Text($"{buffSheet.GetRow(buff)!.Name}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_plugin.Battles[choosed].Name[source]}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{potency}");
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{_plugin.Battles[choosed].DPP(source) * potency / _plugin.Battles[choosed].Duration()}");
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{_plugin.Battles[choosed].TotalDotDamage * _plugin.Battles[choosed].DPP(source) * potency / total / _plugin.Battles[choosed].Duration()}");
                }

                ImGui.EndTable();
                ImGui.Text($"模拟DPS/实际DPS:{total * 100 / _plugin.Battles[choosed].TotalDotDamage - 100}%%");
            }

        }

        public void Dispose()
        {

        }
    }

    public class MainWindow : Window, IDisposable
    {
        private List<Dictionary<uint, long>> savedBattle = new();
        private long startTime = 0;
        private long lastTime = 0;

        public MainWindow(ACT plugin) : base("ACT Main Window")
        {

        }

        public override void Draw()
        {
            
            if (DalamudApi.Conditions[ConditionFlag.PvPDisplayActive]) return;
            if (config.Mini)
            {
                DrawMini();
                return;
            }
            if (_plugin.Battles.Count < 1) return;
            Flags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar |
                    (config.NoResize ? ImGuiWindowFlags.NoResize : ImGuiWindowFlags.None) |
                    (config.Lock ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
            BgAlpha = config.BGColor / 100f;
            {
                var battle = _plugin.Battles[choosed];
                var seconds = battle.Duration();
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.ArrowButton("Mini", ImGuiDir.Left))
                    {
                        config.Mini = !config.Mini;
                        config.WindowSize = ImGui.GetWindowSize();
                        config.Save();
                        return;
                    }

                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("最小化");
                    ImGui.SameLine();
                    var items = new[] { "", "", "", "", "", "" };
                    for (var i = 0; i < _plugin.Battles.Count - 1; i++)
                        items[i] =
                            $"{DateTimeOffset.FromUnixTimeSeconds(_plugin.Battles[i].StartTime).ToLocalTime():t}-{DateTimeOffset.FromUnixTimeSeconds(_plugin.Battles[i].EndTime).ToLocalTime():t} {_plugin.Battles[i].Zone}";
                    try
                    {
                        items[_plugin.Battles.Count - 1] = $"Current: {_plugin.Battles[^1].Zone}";
                    }
                    catch (Exception e)
                    {
                        DalamudApi.Log.Error(e.ToString());
                    }

                    ImGui.SetNextItemWidth(250);
                    ImGui.Combo("##battles", ref choosed, items, _plugin.Battles.Count);
                    if (DalamudApi.ClientState.LocalPlayer != null &&
                        DalamudApi.Conditions[ConditionFlag.InCombat] &&
                        _plugin.Battles[^1].StartTime != 0)
                        _plugin.Battles[^1].EndTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                    ImGui.Text(seconds is > 3600 or <= 1 ? $"00:00" : $"{seconds / 60:00}:{seconds % 60:00}");
                    ImGui.SameLine(ImGui.GetWindowSize().X - 44);
                    if (ImGui.Button(config.HideName ? "" : "")) config.HideName = !config.HideName;
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(config.HideName ? "看" : "藏");
                    ImGui.EndMenuBar();
                }

                if (!config.SaveData) DrawData(battle);
                else DrawDataWithCalc(battle);
            }

        }



        private void CheckSave(Dictionary<uint, long> dmgList)
        {
            if (_plugin.Battles.Count < 1) return;
            var battle = _plugin.Battles[^1];
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (startTime != battle.StartTime)
                //新的战斗
            {
                savedBattle.Clear();
                savedBattle.Add(dmgList);
                startTime = battle.StartTime;
                lastTime = startTime;
            }
            else
            {
                if (savedBattle.Count == 0 || now - lastTime > 0) //过了1秒
                {
                    savedBattle.Add(dmgList);
                    lastTime = now;
                }
            }

            while (savedBattle.Count > config.SaveTime + 2) //删除不必要的数据
            {
                savedBattle.RemoveAt(0);
            }
        }

        private void DrawData(ACTBattle battle)
        {
            long total = 0;
            Dictionary<uint, long> dmgList = new();
            var seconds = battle.Duration();

            foreach (var (actor, damage) in battle.DataDic)
            {
                dmgList.Add(actor, damage.Damages[0].Damage);
                if (float.IsInfinity(battle.TotalDotSim) || battle.TotalDotSim == 0 || battle.Level < 64) continue;
                var dotDamage = (from entry in battle.DotDmgList where (entry.Key & 0xFFFFFFFF) == actor select entry.Value).Sum();
                dmgList[actor] += (long)dotDamage;
            }

            dmgList = (from entry in dmgList orderby entry.Value descending select entry).ToDictionary(x => x.Key, x => x.Value);

            ImGui.BeginTable("ACTMainWindow", 7, ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable);
            {
                ImGui.TableSetupColumn("###Icon",
                    ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide |
                    ImGuiTableColumnFlags.NoDirectResize,
                    ImGui.GetTextLineHeight());
                var headers = new string[]
                    {"角色名", "菜", "直击", "暴击", "直爆"};
                foreach (var t in headers) ImGui.TableSetupColumn(t);

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (45f - ImGui.CalcTextSize("伤害").X) / 2);
                ImGui.TableSetupColumn("伤害", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, 45f);
                ImGui.TableHeadersRow();


                foreach (var (actor, value) in dmgList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (_plugin.Icon.TryGetValue(battle.DataDic[actor].JobId, out var icon))
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));

                    ImGui.TableNextColumn();
                    ImGui.Text(config.HideName
                        ? ((Job)battle.DataDic[actor].JobId).ToString()
                        : battle.Name[actor]);
                    ImGui.TableNextColumn();
                    ImGui.Text(battle.DataDic[actor].Death.ToString("D"));
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].D /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].C /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].DC /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{(float)value / seconds,8:F1}").X);
                    ImGui.Text($"{(float)value / seconds,8:F1}");
                    if (ImGui.IsItemHovered()) DrawDetails(actor);
                    total += value;
                }
                if (battle.TotalDotDamage != 0 && float.IsInfinity(battle.TotalDotSim) ||
                    battle.Level < 64) //Dot damage
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("DOT");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                    ImGui.CalcTextSize(
                    $"{(float)battle.TotalDotDamage / battle.Duration(),8:F1}")
                                            .X);
                    ImGui.Text(
                    $"{(float)battle.TotalDotDamage / battle.Duration(),8:F1}");
                    total += battle.TotalDotDamage;
                }

                if (battle.LimitBreak.Count > 0)
                {
                    long limitDamage = 0;
                    foreach (var (skill, damage) in battle.LimitBreak)
                    {
                        limitDamage += damage;
                    }
                    ImGui.TableNextRow(); //LimitBreak
                    ImGui.TableNextColumn();
                    if (_plugin.Icon.TryGetValue(99, out var icon))
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                    ImGui.TableNextColumn();
                    ImGui.Text("极限技");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{(float)limitDamage / seconds,8:F1}").X);
                    ImGui.Text($"{(float)limitDamage / seconds,8:F1}");
                    if (ImGui.IsItemHovered()) DrawLimitBreak();
                    total += limitDamage;
                }

                ImGui.TableNextRow(); //Total Damage
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                    ImGui.CalcTextSize($"{(float)total / seconds,8:F1}").X);
                ImGui.Text($"{(float)total / seconds,8:F1}");

                if (!float.IsInfinity(battle.TotalDotSim) && battle.TotalDotSim != 0 && config.delta) //Dot Simulation
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Δ");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{battle.TotalDotSim * 100 / battle.TotalDotDamage - 100:F2}%%");
                }
            }
            ImGui.EndTable();
        }

        private void DrawDataWithCalc(ACTBattle battle)
        {
            long total = 0;
            Dictionary<uint, long> dmgList = new();
            var seconds = battle.Duration();
            var index = Math.Min(savedBattle.Count, config.CalcTime + 1) - 1;

            foreach (var (actor, damage) in battle.DataDic)
            {
                dmgList.Add(actor, damage.Damages[0].Damage);
                if (float.IsInfinity(battle.TotalDotSim) || battle.TotalDotSim == 0 || battle.Level < 64) continue;
                var dotDamage = (from entry in battle.DotDmgList where (entry.Key & 0xFFFFFFFF) == actor select entry.Value).Sum();
                dmgList[actor] += (long)dotDamage;
            }

            dmgList = (from entry in dmgList orderby entry.Value descending select entry).ToDictionary(x => x.Key, x => x.Value);

            if (config.SaveData) CheckSave(dmgList);

            ImGui.BeginTable("ACTMainWindow", 8, ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable);
            {
                ImGui.TableSetupColumn("###Icon",
                    ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide |
                    ImGuiTableColumnFlags.NoDirectResize,
                    ImGui.GetTextLineHeight());
                var headers = new string[]
                    {"角色名", "菜", "直击", "暴击", "直爆"};
                foreach (var t in headers) ImGui.TableSetupColumn(t);

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (45f - ImGui.CalcTextSize("伤害").X) / 2);
                ImGui.TableSetupColumn("伤害", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, 45f);
                ImGui.TableSetupColumn("计算伤害", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, 80f);
                ImGui.TableHeadersRow();
                
                foreach (var (actor, value) in dmgList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (_plugin.Icon.TryGetValue(battle.DataDic[actor].JobId, out var icon))
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));

                    ImGui.TableNextColumn();
                    ImGui.Text(config.HideName
                        ? ((Job)battle.DataDic[actor].JobId).ToString()
                        : battle.Name[actor]);
                    ImGui.TableNextColumn();
                    ImGui.Text(battle.DataDic[actor].Death.ToString("D"));
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].D /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].C /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)battle.DataDic[actor].Damages[0].DC /
                                battle.DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{(float)value / seconds,8:F1}").X);
                    ImGui.Text($"{(float)value / seconds,8:F1}");
                    if (ImGui.IsItemHovered()) DrawDetails(actor);

                    ImGui.TableNextColumn();
                    savedBattle[index].TryGetValue(actor, out var later);
                    savedBattle[0].TryGetValue(actor, out var first);
                    var data = ((float)later - first) / (index);

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{data,8:F1}").X);
                    ImGui.Text($"{data,8:F1}");

                    total += value;
                }
                if (battle.TotalDotDamage != 0 && float.IsInfinity(battle.TotalDotSim) ||
                    battle.Level < 64) //Dot damage
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("DOT");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                    ImGui.CalcTextSize(
                    $"{(float)battle.TotalDotDamage / battle.Duration(),8:F1}")
                                            .X);
                    ImGui.Text(
                    $"{(float)battle.TotalDotDamage / battle.Duration(),8:F1}");
                    total += battle.TotalDotDamage;
                    ImGui.TableNextColumn();
                }

                if (battle.LimitBreak.Count > 0)
                {
                    long limitDamage = 0;
                    foreach (var (skill, damage) in battle.LimitBreak)
                    {
                        limitDamage += damage;
                    }
                    ImGui.TableNextRow(); //LimitBreak
                    ImGui.TableNextColumn();
                    if (_plugin.Icon.TryGetValue(99, out var icon))
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                    ImGui.TableNextColumn();
                    ImGui.Text("极限技");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{(float)limitDamage / seconds,8:F1}").X);
                    ImGui.Text($"{(float)limitDamage / seconds,8:F1}");
                    if (ImGui.IsItemHovered()) DrawLimitBreak();
                    ImGui.TableNextColumn();
                    total += limitDamage;
                }

                ImGui.TableNextRow(); //Total Damage
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                    ImGui.CalcTextSize($"{(float)total / seconds,8:F1}").X);
                ImGui.Text($"{(float)total / seconds,8:F1}");
                ImGui.TableNextColumn();

                if (!float.IsInfinity(battle.TotalDotSim) && battle.TotalDotSim != 0 && config.delta) //Dot Simulation
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Δ");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{battle.TotalDotSim * 100 / battle.TotalDotDamage - 100:F2}%%");
                    ImGui.TableNextColumn();
                }
            }
            ImGui.EndTable();
        }


        private void DrawDetails(uint actor)
        {
            ImGui.BeginTooltip();
            ImGui.BeginTable("Tooltip", 6, ImGuiTableFlags.Borders);

            ImGui.TableSetupColumn("###Icon");
            ImGui.TableSetupColumn("###SkillName");
            ImGui.TableSetupColumn("直击");
            ImGui.TableSetupColumn("暴击");
            ImGui.TableSetupColumn("直爆");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (45f - ImGui.CalcTextSize("伤害").X) / 2);
            ImGui.TableSetupColumn("DPS", ImGuiTableColumnFlags.WidthFixed, 45f);
            ImGui.TableHeadersRow();

            var battle = _plugin.Battles[choosed];

            var damage = battle.DataDic[actor].Damages.ToList();
            damage.Sort((pair1, pair2) => pair2.Value.Damage.CompareTo(pair1.Value.Damage));
            foreach (var (action, dmg) in damage)
            {
                if (action == 0 || sheet.GetRow(action) == null) continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (Icon.TryGetValue(action, out var icon))
                    ImGui.Image(icon!.ImGuiHandle,
                        new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                ImGui.TableNextColumn();
                ImGui.Text(sheet.GetRow(action)!.Name);
                ImGui.TableNextColumn();
                ImGui.Text(((float)dmg.D / dmg.swings).ToString("P1") + "%");
                ImGui.TableNextColumn();
                ImGui.Text(((float)dmg.C / dmg.swings).ToString("P1") + "%");
                ImGui.TableNextColumn();
                ImGui.Text(((float)dmg.DC / dmg.swings).ToString("P1") + "%");
                ImGui.TableNextColumn();
                var temp = (float)dmg.Damage / battle.Duration();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                    ImGui.CalcTextSize($"{temp,8:F1}").X);
                ImGui.Text($"{temp,8:F1}");
            }


            if (!float.IsInfinity(battle.TotalDotSim) && battle.TotalDotSim != 0)
            {
                ImGui.TableNextRow();
                var dots = (from dot in battle.DotDmgList where (dot.Key & 0xFFFFFFFF) == actor select dot).ToList();
                foreach (var (active, dotDmg) in dots)
                {
                    var buff = (uint)(active >> 32);
                    var source = (uint)(active & 0xFFFFFFFF);
                    if (!BuffIcon.ContainsKey(buff))
                        BuffIcon.TryAdd(buff,
                            DalamudApi.Textures.GetIcon(buffSheet.GetRow(buff)!.Icon));

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Image(BuffIcon[buff]!.ImGuiHandle,
                        new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight() * 1.2f));
                    ImGui.TableNextColumn();
                    ImGui.Text(buffSheet.GetRow(buff)!.Name);
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    var temp = dotDmg / battle.Duration();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{temp,8:F1}").X);
                    ImGui.Text($"{temp,8:F1}");
                }
            }

            ImGui.EndTable();
            if (battle.DataDic[actor].MaxDamageSkill != 0)
            {
                ImGui.Text($"最大伤害 : {sheet.GetRow(battle.DataDic[actor].MaxDamageSkill)?.Name} - {battle.DataDic[actor].MaxDamage:N0}");
            }
            ImGui.EndTooltip();
        }

        private void DrawLimitBreak()
        {
            ImGui.BeginTooltip();
            ImGui.BeginTable("LimitBreak", 3, ImGuiTableFlags.Borders);

            ImGui.TableSetupColumn("###Icon");
            ImGui.TableSetupColumn("###SkillName");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (45f - ImGui.CalcTextSize("伤害").X) / 2);
            ImGui.TableSetupColumn("DPS", ImGuiTableColumnFlags.WidthFixed, 45f);
            ImGui.TableHeadersRow();

            var damage = _plugin.Battles[choosed].LimitBreak.ToList();
            damage.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            foreach (var (action, dmg) in damage)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (_plugin.Icon.TryGetValue(99, out var icon))
                    ImGui.Image(icon!.ImGuiHandle,
                        new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                ImGui.TableNextColumn();
                ImGui.Text(sheet.GetRow(action)!.Name);
                ImGui.TableNextColumn();
                var temp = (float)dmg / _plugin.Battles[choosed].Duration();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                    ImGui.CalcTextSize($"{temp,8:F1}").X);
                ImGui.Text($"{temp,8:F1}");
            }

            ImGui.EndTable();
            ImGui.EndTooltip();
        }

        private void DrawMini()
        {
            if (config.Mini)
            {
                Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar |
                        (config.NoResize ? ImGuiWindowFlags.NoResize : ImGuiWindowFlags.None) |
                        (config.Lock ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
                
                if (ImGui.ImageButton(mainIcon.ImGuiHandle, new Vector2(40f)))
                {
                    Flags ^= ImGuiWindowFlags.AlwaysAutoResize;
                    config.Mini = !config.Mini;
                    config.Save();
                    ImGui.SetWindowSize(config.WindowSize);
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("还原");
            }
        }

        public void Dispose()
        {

        }
    }

}