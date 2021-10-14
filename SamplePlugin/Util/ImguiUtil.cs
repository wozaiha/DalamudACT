using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using ImGuiScene;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace CEHelper.Util
{
	static class ImguiUtil
	{

		/// <summary>ColorPicker with palette with color picker options.</summary>
		/// <param name="id">Id for the color picker.</param>
		/// <param name="description">The description of the color picker.</param>
		/// <param name="originalColor">The current color.</param>
		/// <param name="flags">Flags to customize color picker.</param>
		/// <returns>Selected color.</returns>
		public static void ColorPickerWithPalette(int id, string description, ref Vector4 originalColor, ImGuiColorEditFlags flags)
		{
			Vector4 col = originalColor;
			List<Vector4> vector4List = ImGuiHelpers.DefaultColorPalette(36);
			if (ImGui.ColorButton(string.Format("{0}###ColorPickerButton{1}", (object)description, (object)id), originalColor, flags))
				ImGui.OpenPopup(string.Format("###ColorPickerPopup{0}", (object)id));
			if (ImGui.BeginPopup(string.Format("###ColorPickerPopup{0}", (object)id)))
			{
				if (ImGui.ColorPicker4(string.Format("###ColorPicker{0}", (object)id), ref col, flags))
				{
					originalColor = col;
				}
				for (int index1 = 0; index1 < 4; ++index1)
				{
					ImGui.Spacing();
					for (int index2 = index1 * 9; index2 < index1 * 9 + 9; ++index2)
					{
						if (ImGui.ColorButton(string.Format("###ColorPickerSwatch{0}{1}{2}", (object)id, (object)index1, (object)index2), vector4List[index2]))
						{
							originalColor = vector4List[index2];
							ImGui.CloseCurrentPopup();
							ImGui.EndPopup();
							return;
						}
						ImGui.SameLine();
					}
				}
				ImGui.EndPopup();
			}
		}

		//public static bool DrawRingWorld(this ImDrawListPtr drawList, Vector3 vector3, float radius, int numSegments, float thicc, uint colour, out Vector2 ringCenter)
		//{
		//	if (!Util.WorldToScreenEx(vector3, out ringCenter, out var z) || z < 0) return false;
		//	var seg = numSegments / 2;
		//	for (var i = 0; i <= numSegments; i++)
		//	{
		//		Util.WorldToScreenEx(
		//			new Vector3(vector3.X + radius * (float)Math.Sin(Math.PI / seg * i), vector3.Y,
		//				vector3.Z + radius * (float)Math.Cos(Math.PI / seg * i)), out var pos, out _);
		//		drawList.PathLineTo(new Vector2(pos.X, pos.Y));
		//	}

		//	drawList.PathStroke(colour, ImDrawFlags.Closed, thicc);
		//	return true;
		//}

		//public static bool DrawRingWorldWithText(this ImDrawListPtr drawList, Vector3 vector3, float radius, int numSegments, float thicc, uint colour, string text, Vector2 offset = default)
		//{
		//	if (!DrawRingWorld(drawList, vector3, radius, numSegments, thicc, colour, out var ringCenter)) return false;
		//	DrawTextWithBorderBg(drawList, ringCenter + offset, text, colour, BuildUi.TransBlack);
		//	//drawList.AddText(ringCenter, colour, text);
		//	return true;
		//}
		//public static void DrawCircleOutlined(this ImDrawListPtr drawList, Vector2 screenPos, uint fgcol, uint bgcol)
		//{
		//	var seg = (int)config.Overlay3D_RingType;
		//	var size = config.Overlay3D_RingSize;
		//	var thick = config.Overlay3D_IconStrokeThickness;
		//	//FDL.AddImageRounded(arrowTex.ImGuiHandle, NVector2.Zero, new NVector2(arrowTex.Width,arrowTex.Height ),
		//	//	NVector2.Zero, new NVector2(arrowTex.Width, arrowTex.Height), red,
		//	//	(screenPos - ImGuiHelpers.MainViewport.GetCenter()).Normalize());
		//	//BDL.AddCircle(screenPos, size + thick, fgcol, seg, thick * 2);
		//	drawList.AddCircleFilled(screenPos, size, fgcol, seg);
		//	drawList.AddCircle(screenPos, size, bgcol, seg, thick / 2);
		//	//BDL.AddCircle(screenPos, size + thick * 2, black, seg, 1);
		//	//BDL.AddCircle(screenPos, size, black, seg, 1);
		//}


		internal static bool IconButton(FontAwesomeIcon icon, string id, Vector2 size)
		{
			ImGui.PushFont(UiBuilder.IconFont);
			bool ret;
			ret = ImGui.Button($"{icon.ToIconString()}##{id}", size);
			ImGui.PopFont();
			return ret;
		}

		internal static bool IconButton(FontAwesomeIcon icon, string id)
		{
			ImGui.PushFont(UiBuilder.IconFont);
			bool ret;
			ret = ImGui.Button($"{icon.ToIconString()}##{id}");
			ImGui.PopFont();
			return ret;
		}
		internal static bool ComboEnum<T>(this T eEnum, string label) where T : Enum
		{
			bool ret = false;
			var enumType = typeof(T);
			var names = Enum.GetNames(enumType);
			var values = Enum.GetValues(enumType);

			ImGui.BeginCombo(label, eEnum.ToString());
			for (int i = 0; i < names.Length; i++)
			{
				if (ImGui.Selectable($"{names[i]}##{label}"))
				{
					eEnum = (T)values.GetValue(i);
					ret = true;
				}
			}
			ImGui.EndCombo();

			return ret;
		}

		public static void DrawText(this ImDrawListPtr drawList, Vector2 pos, string text, uint col, bool stroke, bool centerAlignX = true, uint strokecol = 0xFF000000)
		{
			if (centerAlignX)
			{
				var size = ImGui.CalcTextSize(text);
				pos -= new Vector2(size.X, 0) / 2;
			}

			if (stroke)
			{
				drawList.AddText(pos + new Vector2(-1, -1), strokecol, text);
				drawList.AddText(pos + new Vector2(-1, 1), strokecol, text);
				drawList.AddText(pos + new Vector2(1, -1), strokecol, text);
				drawList.AddText(pos + new Vector2(1, 1), strokecol, text);
			}

			drawList.AddText(pos, col, text);
		}

		public static void DrawTextWithBg(this ImDrawListPtr drawList, Vector2 pos, string text, uint col = 0xFFFFFFFF, uint bgcol = 0xFF000000, bool centerAlignX = true)
		{
			var size = ImGui.CalcTextSize(text) + new Vector2(ImGui.GetStyle().ItemSpacing.X, 0);
			if (centerAlignX)
			{
				pos -= new Vector2(size.X, 0) / 2;
			}

			drawList.AddRectFilled(pos, pos + size, bgcol);
			drawList.AddText(pos + new Vector2(ImGui.GetStyle().ItemSpacing.X / 2, 0), col, text);
		}

		//public static void DrawTextWithBorderBg(this ImDrawListPtr drawList, Vector2 pos, string text, uint col = 0xFFFFFFFF, uint bgcol = 0xFF000000, bool centerAlignX = true)
		//{
		//	var size = ImGui.CalcTextSize(text) + new Vector2(ImGui.GetStyle().ItemSpacing.X, 0);
		//	if (centerAlignX)
		//	{
		//		pos -= new Vector2(size.X, 0) / 2;
		//	}

		//	drawList.AddRectFilled(pos, pos + size, bgcol, RadarPlugin.config.Overlay3D_NamePlateRound);
		//	drawList.AddRect(pos, pos + size, col, RadarPlugin.config.Overlay3D_NamePlateRound);
		//	drawList.AddText(pos + new Vector2(ImGui.GetStyle().ItemSpacing.X / 2 + 0.5f, -0.5f), col, text);
		//}
		public static void DrawArrow(this ImDrawListPtr drawList, Vector2 pos, float size, uint color, uint bgcolor, float rotation, float thickness, float outlinethickness)
		{
			var v = new Vector2[3];
			v[0] = pos + new Vector2(-size - outlinethickness / 2, -0.5f * size - outlinethickness / 2).Rotate(rotation);
			v[1] = pos + new Vector2(0, 0.5f * size).Rotate(rotation);
			v[2] = pos + new Vector2(size + outlinethickness / 2, -0.5f * size - outlinethickness / 2).Rotate(rotation);

			drawList.AddPolyline(ref v[0], 3, bgcolor, ImDrawFlags.RoundCornersDefault,
				thickness + outlinethickness);
			DrawArrow(drawList, pos, size, color, rotation, thickness);
		}

		public static void DrawArrow(this ImDrawListPtr drawList, Vector2 pos, float size, uint color, float rotation, float thickness)
		{
			var v = new Vector2[3];
			v[0] = pos + new Vector2(-size, -0.5f * size).Rotate(rotation);
			v[1] = pos + new Vector2(0, 0.5f * size).Rotate(rotation);
			v[2] = pos + new Vector2(size, -0.5f * size).Rotate(rotation);

			drawList.AddPolyline(ref v[0], 3, color, ImDrawFlags.RoundCornersDefault,
				thickness);
		}

        public static void DrawArrow(this ImDrawListPtr drawList, Vector2 pos, float size, uint color, uint bgcolor, Vector2 rotation, float thickness, float outlinethickness)
        {
            var v = new Vector2[3];
            v[0] = pos + new Vector2(-size - outlinethickness / 2, -0.4f * size - outlinethickness / 2).Rotate(rotation);
            v[1] = pos + new Vector2(0, 0.6f * size).Rotate(rotation);
            v[2] = pos + new Vector2(size + outlinethickness / 2, -0.4f * size - outlinethickness / 2).Rotate(rotation);

            drawList.AddPolyline(ref v[0], 3, bgcolor, ImDrawFlags.RoundCornersDefault,
                thickness + outlinethickness);
            DrawArrow(drawList, pos, size, color, rotation, thickness);
        }

        public static void DrawArrow(this ImDrawListPtr drawList, Vector2 pos, float size, uint color, Vector2 rotation, float thickness)
		{
			var v = new Vector2[3];
			v[0] = pos + new Vector2(-size, -0.4f * size).Rotate(rotation);
			v[1] = pos + new Vector2(0, 0.6f * size).Rotate(rotation);
			v[2] = pos + new Vector2(size, -0.4f * size).Rotate(rotation);
			//FDL.AddCircleFilled(pos, 2, white);
			drawList.AddPolyline(ref v[0], 3, color, ImDrawFlags.RoundCornersDefault,
				thickness);
		}

		public static void DrawTrangle(this ImDrawListPtr drawList, Vector2 pos, float size, uint color, Vector2 rotation, bool filled = true)
		{
			Vector2[] GettriV(Vector2 vin, float s, Vector2 rotation)
			{
				rotation = rotation.Normalize();
				var v1 = new Vector2(0, s * 1.732050807568877f - s * 0.666666666666f);
				var v2 = new Vector2(-s * 0.8f, -s * 0.666666666666f);
				var v3 = new Vector2(s * 0.8f, -s * 0.666666666666f);

				v1 = vin + v1.Rotate(rotation);
				v2 = vin + v2.Rotate(rotation);
				v3 = vin + v3.Rotate(rotation);

				return new Vector2[] { v1, v2, v3 };
			}


			var f = GettriV(pos, size, rotation);

			if (filled)
			{
				drawList.AddTriangleFilled(f[0], f[1], f[2], color);
			}
			else
			{
				drawList.AddTriangle(f[0], f[1], f[2], color);
			}
		}

		//public static void DrawMapDot(this ImDrawListPtr drawList, Vector2 pos, uint col)
		//{
		//	var strokecol = ImGui.GetColorU32(RadarPlugin.config.Overlay2D_StrokeColor);

		//	drawList.AddCircleFilled(pos, RadarPlugin.config.Overlay2D_DotSize, col);

		//	if (RadarPlugin.config.Overlay2D_DotStroke != 0)
		//	{
		//		drawList.AddCircle(pos, RadarPlugin.config.Overlay2D_DotSize, strokecol, 0, RadarPlugin.config.Overlay2D_DotStroke);
		//	}
		//}

		//public static void DrawMapTextDot(this ImDrawListPtr drawList, Vector2 pos, string str, uint fgcolor, uint bgcolor)
		//{
		//	//var strokecol = ImGui.GetColorU32(RadarPlugin.config.Overlay2D_StrokeColor);
		//	if (!string.IsNullOrWhiteSpace(str))
		//	{
		//		drawList.DrawText(pos, str, fgcolor, RadarPlugin.config.Overlay2D_TextStroke, true, bgcolor);
		//	}
		//	drawList.AddCircleFilled(pos, RadarPlugin.config.Overlay2D_DotSize, fgcolor);

		//	if (RadarPlugin.config.Overlay2D_DotStroke != 0)
		//	{
		//		drawList.AddCircle(pos, RadarPlugin.config.Overlay2D_DotSize, bgcolor, 0,
		//			RadarPlugin.config.Overlay2D_DotStroke);
		//	}
		//}

		public static void DrawIcon(this ImDrawListPtr drawlist, Vector2 pos, TextureWrap icon, float size = 1)
		{
			var actualSize = icon.GetSize() * size;
			drawlist.AddImage(icon.ImGuiHandle, pos, pos);
		}

		//public static void DrawIcon(this ImDrawListPtr drawlist, Vector2 pos, TextureWrap icon, ContentAlignment alignment, float size = 1)
		//{
		//	var actualSize = icon.GetSize() * size;
		//	switch (alignment)
		//	{
		//		case ContentAlignment.TopLeft:
		//			break;
		//		case ContentAlignment.TopCenter:
		//			pos.X -= actualSize.X / 2;
		//			break;
		//		case ContentAlignment.TopRight:
		//			pos.X -= actualSize.X;
		//			break;
		//		case ContentAlignment.MiddleLeft:
		//			pos.Y -= actualSize.Y / 2;
		//			break;
		//		case ContentAlignment.MiddleCenter:
		//			pos.X -= actualSize.X / 2;
		//			pos.Y -= actualSize.Y / 2;
		//			break;
		//		case ContentAlignment.MiddleRight:
		//			pos.X -= actualSize.X;
		//			pos.Y -= actualSize.Y / 2;
		//			break;
		//		case ContentAlignment.BottomLeft:
		//			pos.Y -= actualSize.Y;
		//			break;
		//		case ContentAlignment.BottomCenter:
		//			pos.X -= actualSize.X / 2;
		//			pos.Y -= actualSize.Y;
		//			break;
		//		case ContentAlignment.BottomRight:
		//			pos.X -= actualSize.X;
		//			pos.Y -= actualSize.Y;
		//			break;
		//		default:
		//			throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
		//	}
		//	drawlist.AddImage(icon.ImGuiHandle, pos, pos+actualSize);
		//}
	}
}