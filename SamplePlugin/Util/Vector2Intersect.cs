using System.Numerics;
using Dalamud.Interface;

namespace ACT.Util
{
	static class Vector2Intersect
	{
		public static bool GetBorderClampedVector2(Vector2 screenpos, Vector2 clampSize, out Vector2 clampedPos)
		{
			var mainViewport = ImGuiHelpers.MainViewport;
			var screencenter = mainViewport.GetCenter();
			var screenpLT = mainViewport.Pos + clampSize;
			var screenpRT = mainViewport.Pos + new Vector2(mainViewport.Size.X - clampSize.X, clampSize.Y);
			var screenpLB = mainViewport.Pos + new Vector2(clampSize.X, mainViewport.Size.Y - clampSize.Y);
			var screenpRB = mainViewport.Pos + mainViewport.Size - clampSize;

			FindIntersection(screenpLT, screenpRT, screencenter, screenpos, out _, out var segmentsIntersectLTRT, out var vector2LTRT, out _, out _);
			FindIntersection(screenpRT, screenpRB, screencenter, screenpos, out _, out var segmentsIntersectRTRB, out var vector2RTRB, out _, out _);
			FindIntersection(screenpRB, screenpLB, screencenter, screenpos, out _, out var segmentsIntersectRBLB, out var vector2RBLB, out _, out _);
			FindIntersection(screenpLB, screenpLT, screencenter, screenpos, out _, out var segmentsIntersectLBLT, out var vector2LBLT, out _, out _);

			if (segmentsIntersectLTRT)
			{
				clampedPos = vector2LTRT;
			}
			else if (segmentsIntersectRTRB)
			{
				clampedPos = vector2RTRB;
			}
			else if (segmentsIntersectRBLB)
			{
				clampedPos = vector2RBLB;
			}
			else if (segmentsIntersectLBLT)
			{
				clampedPos = vector2LBLT;
			}
			else
			{
				clampedPos = Vector2.Zero;
				return false;
			}

			return true;
		}

		// Find the point of intersection between
		// the lines p1 --> p2 and p3 --> p4.
		private static void FindIntersection(
			Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4,
			out bool lines_intersect, out bool segmentsIntersect,
			out Vector2 intersection,
			out Vector2 closeP1, out Vector2 closeP2)
		{
			// Get the segments' parameters.
			float dx12 = p2.X - p1.X;
			float dy12 = p2.Y - p1.Y;
			float dx34 = p4.X - p3.X;
			float dy34 = p4.Y - p3.Y;

			// Solve for t1 and t2
			float denominator = (dy12 * dx34 - dx12 * dy34);

			float t1 = ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34) / denominator;
			if (float.IsInfinity(t1))
			{
				// The lines are parallel (or close enough to it).
				lines_intersect = false;
				segmentsIntersect = false;
				intersection = new Vector2(float.NaN, float.NaN);
				closeP1 = new Vector2(float.NaN, float.NaN);
				closeP2 = new Vector2(float.NaN, float.NaN);
				return;
			}
			lines_intersect = true;

			float t2 = ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12) / -denominator;

			// Find the point of intersection.
			intersection = new Vector2(p1.X + dx12 * t1, p1.Y + dy12 * t1);

			// The segments intersect if t1 and t2 are between 0 and 1.
			segmentsIntersect =
				((t1 >= 0) && (t1 <= 1) &&
				 (t2 >= 0) && (t2 <= 1));

			// Find the closest points on the segments.
			if (t1 < 0)
			{
				t1 = 0;
			}
			else if (t1 > 1)
			{
				t1 = 1;
			}

			if (t2 < 0)
			{
				t2 = 0;
			}
			else if (t2 > 1)
			{
				t2 = 1;
			}

			closeP1 = new Vector2(p1.X + dx12 * t1, p1.Y + dy12 * t1);
			closeP2 = new Vector2(p3.X + dx34 * t2, p3.Y + dy34 * t2);
		}
	}
}
