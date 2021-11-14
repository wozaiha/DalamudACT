using System;
using System.Numerics;

namespace ACT.Util
{
	public static class MathHelper
	{
		public const float E = (float)System.Math.E;
		public const float Log10E = (float)0.4342944819032f;
		public const float Log2E = (float)1.442695040888f;
		public const float Pi = (float)System.Math.PI;
		public const float PiOver2 = (float)(System.Math.PI / 2.0);
		public const float PiOver4 = (float)(System.Math.PI / 4.0);
		public const float TwoPi = (float)(System.Math.PI * 2.0);

		public static float Barycentric(float value1, float value2, float value3, float amount1, float amount2)
		{
			return value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;
		}

		public static float CatmullRom(float value1, float value2, float value3, float value4, float amount)
		{
			// http://stephencarmody.wikispaces.com/Catmull-Rom+splines

			//value1 *= ((-amount + 2.0f) * amount - 1) * amount * 0.5f;
			//value2 *= (((3.0f * amount - 5.0f) * amount) * amount + 2.0f) * 0.5f;
			//value3 *= ((-3.0f * amount + 4.0f) * amount + 1.0f) * amount * 0.5f;
			//value4 *= ((amount - 1.0f) * amount * amount) * 0.5f;
			//
			//return value1 + value2 + value3 + value4;

			// http://www.mvps.org/directx/articles/catmull/

			float amountSq = amount * amount;
			float amountCube = amountSq * amount;

			// value1..4 = P0..3
			// amount = t
			return ((2.0f * value2 +
				(-value1 + value3) * amount +
				(2.0f * value1 - 5.0f * value2 + 4.0f * value3 - value4) * amountSq +
				(3.0f * value2 - 3.0f * value3 - value1 + value4) * amountCube) * 0.5f);
		}

		public static float Clamp(float value, float min, float max)
		{
			return Math.Min(Math.Max(min, value), max);
		}

		public static float Distance(float value1, float value2)
		{
			return Math.Abs(value1 - value2);
		}

		public static float Hermite(float value1, float tangent1, float value2, float tangent2, float amount)
		{
			//http://www.cubic.org/docs/hermite.htm
			float s = amount;
			float s2 = s * s;
			float s3 = s2 * s;
			float h1 = 2 * s3 - 3 * s2 + 1;
			float h2 = -2 * s3 + 3 * s2;
			float h3 = s3 - 2 * s2 + s;
			float h4 = s3 - s2;
			return value1 * h1 + value2 * h2 + tangent1 * h3 + tangent2 * h4;
		}

		public static float Lerp(float value1, float value2, float amount)
		{
			return value1 + (value2 - value1) * amount;
		}

		public static float Max(float value1, float value2)
		{
			return Math.Max(value1, value2);
		}

		public static float Min(float value1, float value2)
		{
			return Math.Min(value1, value2);
		}

		public static float CalculateHeading(Vector3 to, Vector3 from)
		{
			return Rotation(to - from);
		}
		public static float Rotation(Vector3 direction)
		{
			return NormalizeRadian((float)Math.Atan2(direction.X, direction.Z));
		}

		public static float NormalizeRadian(float rad)
		{
			if (rad < 0)
				return -(-rad % TwoPi) + TwoPi;
			return rad % TwoPi;
		}

		public static float SmoothStep(float value1, float value2, float amount)
		{
			//FIXME: check this
			//the function is Smoothstep (http://en.wikipedia.org/wiki/Smoothstep) but the usage has been altered
			// to be similar to Lerp
			amount = amount * amount * (3f - 2f * amount);
			return value1 + (value2 - value1) * amount;
		}

		public static float ToDegrees(float radians)
		{
			return radians * (180f / Pi);
		}

		public static float ToRadians(float degrees)
		{
			return degrees * (Pi / 180f);
		}

		public static float WrapAngle(float angle)
		{
			double num = Math.IEEERemainder(angle, Math.PI * 2.0);
			if (num <= -Math.PI)
			{
				num += Math.PI * 2.0;
			}
			else if (num > Math.PI)
			{
				num -= Math.PI * 2.0;
			}
			return (float)num;
		}
	}
}
