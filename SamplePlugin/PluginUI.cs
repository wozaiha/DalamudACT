using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CEHelper.Util;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Network;
using Dalamud.Interface.Colors;
using Dalamud.Logging;
using ImGuiNET;

namespace CEHelper
{
    internal class PluginUI : IDisposable
    {
        private Configuration config;

        private bool Visible;
        private CEHelper _plugin;
        private Dictionary<uint, Vector2> fateRotation = new();
        private long battleTime = 0;

        private static unsafe ref float HRotation => ref *(float*)(Marshal.ReadIntPtr(
            DalamudApi.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 45 33 C0 33 D2 C6 40 09 01")) + 0x130);

        public PluginUI(CEHelper p)
        {
            _plugin = p;
            config = p.Configuration;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            DalamudApi.PluginInterface.UiBuilder.Draw += DrawACT;
        }

        public void Dispose()
        {
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            DalamudApi.PluginInterface.UiBuilder.Draw -= DrawACT;
        }


        private async void UpdateRotate()
        {
            await Task.Run(() =>
            {
                foreach (var fate in DalamudApi.FateTable)
                {
                    var rot = (fate.Position - DalamudApi.ClientState.LocalPlayer.Position).ToVector2().Normalize()
                        .Rotate(-HRotation);

                    if (fateRotation.ContainsKey(fate.FateId)) fateRotation[fate.FateId] = rot;
                    else fateRotation.Add(fate.FateId, rot);
                }
            });
        }

        public void DrawConfigUI()
        {
            Visible = true;
        }

        private void Draw()
        {
            DrawConfig();
            DrawUI();
        }

        private void DrawConfig()
        {
            if (!Visible) return;

            if (!ImGui.Begin("Config", ref Visible, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                return;
            }

            var changed = false;
            changed |= ImGui.Checkbox("Enabled", ref config.Enabled);
            changed |= ImGui.Checkbox("Locked", ref config.Locked);
            changed |= ImGui.Checkbox("Show only One zone", ref config.LevelEnabled);
            if (config.LevelEnabled)
            {
                changed |= ImGui.Combo("FateLevel", ref config.FateLevel, new[] { "Low", "MIDDLE", "TOP" }, 3);
            }

            if (!config.Locked)
            {
                ImGui.SetNextWindowSize(new Vector2(150, 400));
                ImGui.SetNextWindowPos(config.WindowPos, ImGuiCond.Once);
                ImGui.Begin("Position",
                    ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar);
                config.WindowPos = ImGui.GetWindowPos();
                ImGui.End();
            }

            if (changed) _plugin.Configuration.Save();

            ImGui.End();
        }

        private void DebugTableCell(string value, float[] sizes, bool nextColumn = true)
        {
            var width = ImGui.CalcTextSize(value).X;
            var columnIndex = ImGui.GetColumnIndex();
            var largest = sizes[columnIndex];
            if (width > largest)
                sizes[columnIndex] = width;
            ImGui.Text(value);

            if (nextColumn)
                ImGui.NextColumn();
        }

        int FateLevel(Fate fate)
        {
            if (fate.FateId < 1717) return -1;
            if (fate.FateId < 1725) return 0;
            if (fate.FateId < 1733) return 1;
            if (fate.FateId < 1743) return 2;
            return -1;
        }

        private void DrawUI()
        {
            if (!config.Enabled) return;
            //if (DalamudApi.ClientState.TerritoryType != 975) return;

            const ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize |
                                                 ImGuiWindowFlags.AlwaysAutoResize |
                                                 ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoInputs |
                                                 ImGuiWindowFlags.NoNav |
                                                 ImGuiWindowFlags.NoBringToFrontOnFocus |
                                                 ImGuiWindowFlags.NoSavedSettings;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 2);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 2);

            var pos = config.WindowPos;

            //float ConvertRawPositionToMapCoordinate(int pos, float scale)
            //{
            //	float num1 = scale / 100f;
            //	float num2 = (float)((double)pos * (double)num1 / 1000.0);
            //	return (float)(41.0 / (double)num1 * (((double)num2 + 1024.0) / 2048.0) + 1.0);
            //}

            if (Task.CurrentId == null) UpdateRotate();
            foreach (var fate in DalamudApi.FateTable)
            {
                if (config.LevelEnabled)
                {
                    if (FateLevel(fate) != config.FateLevel) continue;
                }

                var color = fate.State switch
                {
                    FateState.Preparation => ImGuiColors.DPSRed,
                    (FateState)6 => ImGuiColors.DPSRed,
                    FateState.Ended => ImGuiColors.DalamudOrange,
                    FateState.WaitingForEnd => ImGuiColors.DalamudOrange,
                    _ => ImGuiColors.DalamudWhite
                };

                ImGui.PushStyleColor(ImGuiCol.Border, color);
                ImGui.SetNextWindowPos(pos);
                ImGui.SetNextWindowSizeConstraints(
                    new Vector2(150, float.MinValue),
                    new Vector2(150, float.MaxValue));

                if (ImGui.Begin($"{fate.Name}", windowFlags))
                {
                    //Vector3 fatePos = new Vector3(fate.Position.X, fate.Position.Z, fate.Position.Y);
                    var fatePos = fate.Position;
                    var arrpos = ImGui.GetWindowPos() /*+ ImGui.GetCursorPos()*/ +
                                 new Vector2(ImGui.GetTextLineHeight() + 5, ImGui.GetTextLineHeight());
                    ImGui.GetWindowDrawList().DrawArrow(arrpos, ImGui.GetTextLineHeightWithSpacing() * 0.500f,
                        ImGui.ColorConvertFloat4ToU32(color),
                        fateRotation.ContainsKey(fate.FateId) ? fateRotation[fate.FateId] : Vector2.Zero, 5);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetTextLineHeight() +
                                        ImGui.GetTextLineHeightWithSpacing());
                    ImGui.TextColored(color, $"{fate.Name}");
                    ImGui.Separator();

                    var dis = fatePos.Distance2D(DalamudApi.ClientState.LocalPlayer.Position);
                    ImGui.TextUnformatted($"{dis:0.0}");
                    ImGui.SameLine(70);
                    var time = fate.TimeRemaining;
                    if (color != ImGuiColors.DPSRed)
                    {
                        ImGui.Text($"{time / 60:00}:{time % 60:00}");
                        ImGui.SameLine(120);
                        ImGui.Text($"{fate.Progress}%%");
                    }
                    else
                    {
                        ImGui.TextColored(color, "准备中");
                    }

                    pos += new Vector2(0, ImGui.GetWindowSize().Y);

                    ImGui.End();
                }

                ImGui.PopStyleColor();
            }

            ImGui.PopStyleVar(2);
        }

        void DrawACT()
        {
            if (_plugin.Battles.Count < 1) return;
            ImGui.Begin("Damage", ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoTitleBar);
            ImGui.BeginMenuBar();

            if (DalamudApi.ClientState.LocalPlayer != null && (DalamudApi.ClientState.LocalPlayer.StatusFlags & StatusFlags.InCombat) != 0 &&
                _plugin.Battles[^1].StartTime != 0) _plugin.Battles[^1].EndTime = DateTimeOffset.Now.ToUnixTimeSeconds();

            var seconds = _plugin.Battles[^1].Duration();
            if (seconds is > 3600 or < 1)
            {
                ImGui.Text($"00:00");
                seconds = 1;
            }
            else ImGui.Text($"{seconds / 60:00}:{seconds % 60:00}");

            ImGui.SameLine(ImGui.GetWindowSize().X - 50);
            if (ImGui.Button("Reset"))
            {
                _plugin.Battles.Clear();
                _plugin.Battles.Add(new CEHelper.ACTBattle(0,
                    0, new Dictionary<string, long>()));
            }

            ImGui.EndMenuBar();
            long total = 0;
            var damage = _plugin.Battles[^1].Damage.ToList();
            damage.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            foreach (var (key, value) in damage)
            {
                ImGui.Text(key);
                ImGui.SameLine(100);
                ImGui.Text(((float)value / seconds).ToString("0.0"));
                total += value;
            }
            ImGui.Text("总计");
            ImGui.SameLine(100);
            ImGui.Text(((float)total / seconds).ToString("0.0"));

            ImGui.End();
        }

    }
}
