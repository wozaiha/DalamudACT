using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace ACT
{
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

        public PluginUI(ACT p)
        {
            _plugin = p;
            config = p.Configuration;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            foreach (var (_, texture) in Icon) texture?.Dispose();

            foreach (var (_, texture) in BuffIcon) texture?.Dispose();
        }


        public void DrawConfigUI()
        {
            Visible = true;
        }

        private void Draw()
        {
            DrawConfig();
            DrawACT();
            OnBuildUi_Debug();
        }

        private void DrawConfig()
        {
            //if (!Visible) return;

            //if (!ImGui.Begin("Config", ref Visible, ImGuiWindowFlags.NoCollapse))
            //{
            //    ImGui.End();
            //    return;
            //}

            //var changed = false;
            //changed |= ImGui.Checkbox("Enabled", ref config.Enabled);
            //changed |= ImGui.Checkbox("Locked", ref config.Locked);
            //changed |= ImGui.Checkbox("Show only One zone", ref config.LevelEnabled);
            //if (config.LevelEnabled)
            //    changed |= ImGui.Combo("FateLevel", ref config.FateLevel, new[] { "Low", "MIDDLE", "TOP" }, 3);

            //if (!config.Locked)
            //{
            //    ImGui.SetNextWindowSize(new Vector2(150, 400));
            //    ImGui.SetNextWindowPos(config.WindowPos, ImGuiCond.Once);
            //    ImGui.Begin("Position",
            //        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar);
            //    config.WindowPos = ImGui.GetWindowPos();
            //    ImGui.End();
            //}

            //if (changed) _plugin.Configuration.Save();

            //ImGui.End();
        }

        private void DrawDetails(uint actor, float totalDotSim)
        {
            ImGui.BeginTooltip();
            
            if (ImGui.BeginTable("Tooltip", 6,ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("###Icon");
                ImGui.TableSetupColumn("###Name");
                ImGui.TableSetupColumn("直击");
                ImGui.TableSetupColumn("暴击");
                ImGui.TableSetupColumn("直爆");
                ImGui.TableSetupColumn("    DPS", ImGuiTableColumnFlags.WidthFixed, 45f);
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
                            var temp = _plugin.Battles[choosed].TotalDotDamage * _plugin.Battles[choosed].PDD(source) *
                                potency / totalDotSim / _plugin.Battles[choosed].Duration();
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() -
                                                ImGui.CalcTextSize($"{temp,8:F1}").X);
                            ImGui.Text($"{temp,8:F1}");

                        }
                    }
                }
                ImGui.EndTable();
            }

            ImGui.EndTooltip();
        }

        private void DrawACT()
        {
            if (_plugin.Battles.Count < 1) return;
            ImGui.Begin("Damage Beta", ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar);

            var x = ImGui.GetWindowWidth() - 100;
            ImGui.BeginMenuBar();
            var items = new []{"","","","","",""};
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
           
            ImGui.SetNextItemWidth(160);
            ImGui.Combo("##battles", ref choosed, items, _plugin.Battles.Count);
            if (DalamudApi.ClientState.LocalPlayer != null &&
                DalamudApi.Condition[ConditionFlag.InCombat] &&
                _plugin.Battles[^1].StartTime != 0)
                _plugin.Battles[^1].EndTime = DateTimeOffset.Now.ToUnixTimeSeconds();

            var seconds = _plugin.Battles[choosed].Duration();
            if (seconds is > 3600 or <= 1)
            {
                ImGui.Text($"00:00");
            }
            else
            {
                ImGui.Text($"{seconds / 60:00}:{seconds % 60:00}");
            }

            //ImGui.SameLine(ImGui.GetWindowSize().X - 50);
            //if (ImGui.Button("Reset"))
            //{
            //    choosed = 0;
            //    _plugin.Battles.Clear();
            //    _plugin.Battles.Add(new (0, 0));
            //}

            ImGui.EndMenuBar();

            

            long total = 0;
            var totaldotSim = 0f;
            DotDictionary = new Dictionary<uint, float>();
            List<(uint, long)> dmgList = new();
            //PluginLog.Information($"{_plugin.Battles[^1].Name.Count}");
            foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
            {
                var source = (uint)(active & 0xFFFFFFFF);
                var dmg = _plugin.Battles[choosed].PDD(source) * potency;
                totaldotSim += dmg;
                if (DotDictionary.ContainsKey(source)) DotDictionary[source] += dmg;
                else DotDictionary.Add(source, dmg);
            }

            foreach (var (actor, damage) in _plugin.Battles[choosed].DataDic)
                if (!float.IsInfinity(totaldotSim) && totaldotSim != 0 &&
                    DotDictionary.ContainsKey(actor) && _plugin.Battles[choosed].Level >= 64 &&
                    DotDictionary.TryGetValue(actor, out var dotDamage))
                    dmgList.Add((actor, damage.Damages[0].Damage + (long)dotDamage));
                else dmgList.Add((actor, damage.Damages[0].Damage));

            dmgList.Sort((pair1, pair2) => pair2.Item2.CompareTo(pair1.Item2));

            if (ImGui.BeginTable("ACTMainWindow", 7))
            {
                ImGui.TableSetupColumn("###Icon",ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeight());
                var headers = new string[]
                    {"###Name", "D", "C", "DC", "Damage", "Death"};
                foreach (var t in headers) ImGui.TableSetupColumn(t);

                ImGui.TableHeadersRow();


                foreach (var (actor, value) in dmgList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (_plugin.Icon.TryGetValue(_plugin.Battles[choosed].DataDic[actor].JobId, out var icon))
                    {
                        ImGui.Image(icon!.ImGuiHandle,
                            new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                        
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text(_plugin.Battles[choosed].Name[actor]);
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)_plugin.Battles[choosed].DataDic[actor].Damages[0].D / _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1")+"%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)_plugin.Battles[choosed].DataDic[actor].Damages[0].C / _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float)_plugin.Battles[choosed].DataDic[actor].Damages[0].DC / _plugin.Battles[choosed].DataDic[actor].Damages[0].swings).ToString("P1") + "%");
                    ImGui.TableNextColumn();
                    ImGui.Text(((float) value / seconds).ToString("0.0"));
                    if (ImGui.IsItemHovered()) DrawDetails(actor, totaldotSim);
                    total += value;
                    ImGui.TableNextColumn();
                    ImGui.Text(_plugin.Battles[choosed].DataDic[actor].Death.ToString("D"));
                }

                ImGui.TableNextRow();
                if (_plugin.Battles[choosed].TotalDotDamage != 0 && float.IsInfinity(totaldotSim) ||
                    _plugin.Battles[choosed].Level < 64)
                {
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("DOT");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text(((float) _plugin.Battles[choosed].TotalDotDamage / _plugin.Battles[choosed].Duration())
                        .ToString("0.0"));
                    total += _plugin.Battles[choosed].TotalDotDamage;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text("Total");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.Text(((float) total / seconds).ToString("0.0"));

                if (!float.IsInfinity(totaldotSim) && totaldotSim != 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text("Δ");
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text($"{totaldotSim * 100 / _plugin.Battles[choosed].TotalDotDamage - 100:F2}%%");
                }
                ImGui.EndTable();
            }

            ImGui.End();
        }


        private void OnBuildUi_Debug()
        {
            if (!Visible) return;
            if (ImGui.Begin("Debug", ref Visible))
            {
                ImGui.Text(
                    $"Total Dot DPS:{_plugin.Battles[choosed].TotalDotDamage / _plugin.Battles[choosed].Duration()}");

                if (ImGui.BeginTable("Pot", 7))
                {
                    var headers = new string[]
                        { "Name", "ActorId", "PotSkill", "SkillPotency", "Speed", "Special", "PDD" };
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
                        ImGui.Text($"{damage.Special}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{_plugin.Battles[choosed].PDD(actor)}");
                    }
                }

                ImGui.EndTable();
                ImGui.Separator();

                if (ImGui.BeginTable("Dots", 5))
                {
                    var headers = new string[] { "BuffId", "Source", "Potency", "Simulated DPS", "Split DPS" };

                    foreach (var t in headers) ImGui.TableSetupColumn(t);

                    ImGui.TableHeadersRow();
                    var total = 0f;
                    foreach (var (active, potency) in _plugin.Battles[choosed].PlayerDotPotency)
                    {
                        var source = (uint)(active & 0xFFFFFFFF);
                        total += _plugin.Battles[choosed].PDD(source) * potency;
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
                            $"{_plugin.Battles[choosed].PDD(source) * potency / _plugin.Battles[choosed].Duration()}");
                        ImGui.TableNextColumn();
                        ImGui.Text(
                            $"{_plugin.Battles[choosed].TotalDotDamage * _plugin.Battles[choosed].PDD(source) * potency / total / _plugin.Battles[choosed].Duration()}");
                    }

                    ImGui.EndTable();
                    ImGui.Text($"模拟DPS/实际DPS:{total * 100 / _plugin.Battles[choosed].TotalDotDamage - 100}%%");
                }

                ImGui.Separator();

                if (ImGui.BeginTable("Active Dots", 3))
                {
                    var headers = new string[] { "BuffId", "Source", "Target" };

                    foreach (var t in headers) ImGui.TableSetupColumn(t);

                    ImGui.TableHeadersRow();

                    foreach (var active in _plugin.Battles[choosed].ActiveDots)
                    {
                        var dot = ACTBattle.ActiveToDot(active);
                        ImGui.TableNextColumn();
                        ImGui.Text($"{buffSheet.GetRow(dot.BuffId)!.Name}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{_plugin.Battles[choosed].Name[dot.Source]}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{dot.Target:X}");
                    }
                }

                ImGui.EndTable();
                ImGui.Separator();
            }

            ImGui.End();
        }
    }
}
