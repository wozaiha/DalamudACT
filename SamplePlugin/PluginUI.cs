using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SharpDX.DXGI;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DalamudACT;

internal class PluginUI : IDisposable
{
    private Configuration config;

    private bool Visible = false;
    private ACT _plugin;
    public int choosed;
    private ExcelSheet<Action> sheet = DalamudApi.DataManager.GetExcelSheet<Action>();
    public static Dictionary<uint, TextureWrap?> Icon = new();
    private ExcelSheet<Status> buffSheet = DalamudApi.DataManager.GetExcelSheet<Status>();
    public static Dictionary<uint, TextureWrap?> BuffIcon = new();
    private Dictionary<uint, float> DotDictionary;
    private TextureWrap? mainIcon;
    private bool showDebug = false;

    public PluginUI(ACT p)
    {
        _plugin = p;
        config = p.Configuration;

        mainIcon = File.Exists(DalamudApi.PluginInterface.AssemblyLocation.Directory?.FullName + "\\images\\DDD.png")
            ? DalamudApi.PluginInterface.UiBuilder.LoadImage(
                DalamudApi.PluginInterface.AssemblyLocation.Directory?.FullName + "\\images\\DDD.png")
            : DalamudApi.DataManager.GetImGuiTextureHqIcon(62142);

        DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

        foreach (var (_, texture) in Icon) texture?.Dispose();

        foreach (var (_, texture) in BuffIcon) texture?.Dispose();
        mainIcon?.Dispose();
    }


    public void DrawConfigUI()
    {
        Visible = true;
    }

    public void ShowDebug()
    {
        showDebug = !showDebug;
    }

    private void Draw()
    {
        DrawConfig();
        DrawACT();
        OnBuildUi_Debug();
    }

    private void DrawConfig()
    {
        var changed = false;
        if (!Visible) return;
        ImGui.Begin("ConfigWindow", ref Visible, ImGuiWindowFlags.AlwaysAutoResize);
        changed |= ImGui.Checkbox("Lock MainWindow Position", ref config.Lock);
        changed |= ImGui.Checkbox("No Resize", ref config.NoResize);
        changed |= ImGui.InputInt("BackGround Alpha", ref config.BGColor, 1, 1);
        changed |= ImGui.Checkbox("Show Delta", ref config.delta);
        if (changed) config.Save();
        ImGui.End();
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

    private void DrawDetails(uint actor, float totalDotSim)
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

        var damage = _plugin.Battles[choosed].DataDic[actor].Damages.ToList();
        damage.Sort((pair1, pair2) => pair2.Value.Damage.CompareTo(pair1.Value.Damage));
        foreach (var (action, dmg) in damage)
        {
            if (action == 0 || sheet.GetRow(action) == null) continue;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (Icon.TryGetValue(action, out var icon))
                ImGui.Image(icon.ImGuiHandle,
                    new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
            ImGui.TableNextColumn();
            ImGui.Text(sheet.GetRow(action)!.Name);
            ImGui.TableNextColumn();
            ImGui.Text(((float) dmg.D / dmg.swings).ToString("P1") + "%");
            ImGui.TableNextColumn();
            ImGui.Text(((float) dmg.C / dmg.swings).ToString("P1") + "%");
            ImGui.TableNextColumn();
            ImGui.Text(((float) dmg.DC / dmg.swings).ToString("P1") + "%");
            ImGui.TableNextColumn();
            var temp = (float) dmg.Damage / _plugin.Battles[choosed].Duration();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                ImGui.CalcTextSize($"{temp,8:F1}").X);
            ImGui.Text($"{temp,8:F1}");
        }


        if (!float.IsInfinity(totalDotSim) && totalDotSim != 0)
        {
            ImGui.TableNextRow();
            foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
            {
                var buff = (uint) (active >> 32);
                var source = (uint) (active & 0xFFFFFFFF);
                if (source == actor)
                {
                    if (!BuffIcon.ContainsKey(buff))
                        BuffIcon.TryAdd(buff,
                            DalamudApi.DataManager.GetImGuiTextureHqIcon(buffSheet.GetRow(buff)!.Icon));
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
                    var temp = _plugin.Battles[choosed].TotalDotDamage * _plugin.Battles[choosed].DPP(source) *
                        potency / totalDotSim / _plugin.Battles[choosed].Duration();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{temp,8:F1}").X);
                    ImGui.Text($"{temp,8:F1}");
                }
            }
        }

        ImGui.EndTable();
        ImGui.EndTooltip();
    }

    private void DrawACT()
    {
        if (DalamudApi.Condition[ConditionFlag.PvPDisplayActive]) return;
        ImGui.SetNextWindowBgAlpha((float) config.BGColor / 100);
        if (config.Mini)
        {
            ImGui.Begin("Damage Beta",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar |
                (config.NoResize ? ImGuiWindowFlags.NoResize : ImGuiWindowFlags.None) |
                (config.Lock ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None));

            if (ImGui.ImageButton(mainIcon.ImGuiHandle, new Vector2(40f)))
            {
                config.Mini = !config.Mini;
                ImGui.SetWindowSize(config.WindowSize);
                config.Save();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("还原");

            ImGui.End();
            return;
        }


        if (config.Mini) return;
        if (_plugin.Battles.Count < 1) return;
        ImGui.Begin("Damage Beta",
            ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar |
            (config.NoResize ? ImGuiWindowFlags.NoResize : ImGuiWindowFlags.None) |
            (config.Lock ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None));
        {
            var seconds = _plugin.Battles[choosed].Duration();
            ImGui.BeginMenuBar();
            {
                if (ImGui.ArrowButton("Mini", ImGuiDir.Left))
                {
                    config.Mini = !config.Mini;
                    config.WindowSize = ImGui.GetWindowSize();
                    config.Save();
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("最小化");
                ImGui.SameLine();
                var items = new[] {"", "", "", "", "", ""};
                for (var i = 0; i < _plugin.Battles.Count - 1; i++)
                    items[i] =
                        $"{DateTimeOffset.FromUnixTimeSeconds(_plugin.Battles[i].StartTime).ToLocalTime():t}-{DateTimeOffset.FromUnixTimeSeconds(_plugin.Battles[i].EndTime).ToLocalTime():t} {_plugin.Battles[i].Zone}";
                // PluginLog.Information(items[i]);
                try
                {
                    items[_plugin.Battles.Count - 1] = $"Current: {_plugin.Battles[_plugin.Battles.Count - 1].Zone}";
                }
                catch (Exception e)
                {
                    PluginLog.Error(e.ToString());
                }

                ImGui.SetNextItemWidth(250);
                ImGui.Combo("##battles", ref choosed, items, _plugin.Battles.Count);
                if (DalamudApi.ClientState.LocalPlayer != null &&
                    DalamudApi.Condition[ConditionFlag.InCombat] &&
                    _plugin.Battles[^1].StartTime != 0)
                    _plugin.Battles[^1].EndTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                ImGui.Text(seconds is > 3600 or <= 1 ? $"00:00" : $"{seconds / 60:00}:{seconds % 60:00}");
                ImGui.SameLine(ImGui.GetWindowSize().X - 44);
                if (ImGui.Button(config.HideName ? "" : "")) config.HideName = !config.HideName;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(config.HideName ? "看" : "藏");
            }
            ImGui.EndMenuBar();


            long total = 0;
            var totaldotSim = 0f;
            DotDictionary = new Dictionary<uint, float>();
            List<(uint, long)> dmgList = new();
            //PluginLog.Information($"{_plugin.Battles[^1].Name.Count}");
            foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
            {
                var source = (uint) (active & 0xFFFFFFFF);
                var dmg = _plugin.Battles[choosed].DPP(source) * potency;
                totaldotSim += dmg;
                if (DotDictionary.ContainsKey(source)) DotDictionary[source] += dmg;
                else DotDictionary.Add(source, dmg);
            }

            foreach (var (actor, damage) in _plugin.Battles[choosed].DataDic)
                if (!float.IsInfinity(totaldotSim) && totaldotSim != 0 &&
                    DotDictionary.ContainsKey(actor) && _plugin.Battles[choosed].Level >= 64 &&
                    DotDictionary.TryGetValue(actor, out var dotDamage))
                    dmgList.Add((actor, damage.Damages[0].Damage + (long) dotDamage));
                else dmgList.Add((actor, damage.Damages[0].Damage));

            dmgList.Sort((pair1, pair2) => pair2.Item2.CompareTo(pair1.Item2));

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
                    if (_plugin.Icon.TryGetValue(_plugin.Battles[choosed].DataDic[actor].JobId, out var icon))
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));

                    ImGui.TableNextColumn();
                    ImGui.Text(config.HideName
                        ? ((Job) _plugin.Battles[choosed].DataDic[actor].JobId).ToString()
                        : _plugin.Battles[choosed].Name[actor]);
                    ImGui.TableNextColumn();
                    ImGui.Text(_plugin.Battles[choosed].DataDic[actor].Death.ToString("D"));
                    ImGui.TableNextColumn();
                    ImGui.Text(((float) _plugin.Battles[choosed].DataDic[actor].Damages[0].D /
                                _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float) _plugin.Battles[choosed].DataDic[actor].Damages[0].C /
                                _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float) _plugin.Battles[choosed].DataDic[actor].Damages[0].DC /
                                _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                        ImGui.CalcTextSize($"{(float) value / seconds,8:F1}").X);
                    ImGui.Text($"{(float) value / seconds,8:F1}");
                    if (ImGui.IsItemHovered()) DrawDetails(actor, totaldotSim);
                    total += value;
                }


                if (_plugin.Battles[choosed].TotalDotDamage != 0 && float.IsInfinity(totaldotSim) ||
                    _plugin.Battles[choosed].Level < 64) //Dot damage
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
                                                $"{(float) _plugin.Battles[choosed].TotalDotDamage / _plugin.Battles[choosed].Duration(),8:F1}")
                                            .X);
                    ImGui.Text(
                        $"{(float) _plugin.Battles[choosed].TotalDotDamage / _plugin.Battles[choosed].Duration(),8:F1}");
                    total += _plugin.Battles[choosed].TotalDotDamage;
                }

                if (_plugin.Battles[choosed].LimitBreak.Count > 0)
                {
                    long limitDamage = 0;
                    foreach (var (skill,damage) in _plugin.Battles[choosed].LimitBreak)
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
                                    ImGui.CalcTextSize($"{(float) total / seconds,8:F1}").X);
                ImGui.Text($"{(float) total / seconds,8:F1}");

                if (!float.IsInfinity(totaldotSim) && totaldotSim != 0 && config.delta) //Dot Simulation
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
                    ImGui.Text($"{totaldotSim * 100 / _plugin.Battles[choosed].TotalDotDamage - 100:F2}%%");
                }
            }
            ImGui.EndTable();
        }
        ImGui.End();
    }

    private void OnBuildUi_Debug()
    {
        if (!showDebug) return;
        ImGui.Begin("Debug", ref showDebug);
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
                var headers = new string[] {"BuffId", "Source", "Potency", "Simulated DPS", "Split DPS"};

                foreach (var t in headers) ImGui.TableSetupColumn(t);

                ImGui.TableHeadersRow();
                var total = 0f;
                foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
                {
                    var source = (uint) (active & 0xFFFFFFFF);
                    total += _plugin.Battles[choosed].DPP(source) * potency;
                }

                foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
                {
                    ImGui.TableNextColumn();
                    var buff = (uint) (active >> 32);
                    var source = (uint) (active & 0xFFFFFFFF);
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
        ImGui.End();
    }
}