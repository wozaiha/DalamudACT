using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Newtonsoft.Json;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ACT.Util
{
	static class Util
	{
		public static System.Numerics.Vector2 Convert(this SharpDX.Vector2 v) => new(v.X, v.Y);
		public static System.Numerics.Vector3 Convert(this SharpDX.Vector3 v) => new(v.X, v.Y, v.Z);

		public static SharpDX.Vector2 Convert(this System.Numerics.Vector2 v) => new(v.X, v.Y);
		public static SharpDX.Vector3 Convert(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

		public static System.Numerics.Vector3 Convert(this FFXIVClientStructs.FFXIV.Client.Graphics.Vector3 p) => new(p.X, p.Z, p.Y);



		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr GetMatrixSingletonDelegate();

		public static GetMatrixSingletonDelegate GetMatrix;


		public static Vector2 GetSize(this TextureWrap textureWrap) => new Vector2(textureWrap.Width, textureWrap.Height);

		public static Vector2 ToVector2(this Vector3 v) => new Vector2(v.X, v.Z);
		public static float Distance(this Vector3 v, Vector3 v2)
		{
			try
			{
				return (v - v2).Length();
			}
			catch (Exception e)
			{
				return 0;
			}
		}
		public static float Distance2D(this Vector3 v, Vector3 v2)
		{
			try
			{
				return new Vector2(v.X - v2.X, v.Z - v2.Z).Length();
			}
			catch (Exception e)
			{
				return 0;
			}
		}
		public static float Distance2D(this Vector3 v, SharpDX.Vector3 v2)
		{
			try
			{
				return new Vector2(v.X - v2.X, v.Z - v2.Z).Length();
			}
			catch (Exception e)
			{
				return 0;
			}
		}
		public static float Distance2D(this SharpDX.Vector3 v, Vector3 v2)
		{
			try
			{
				return new Vector2(v.X - v2.X, v.Z - v2.Z).Length();
			}
			catch (Exception e)
			{
				return 0;
			}
		}
		public static float Distance2D(this SharpDX.Vector3 v, SharpDX.Vector3 v2)
		{
			try
			{
				return new Vector2(v.X - v2.X, v.Z - v2.Z).Length();
			}
			catch (Exception e)
			{
				return 0;
			}
		}
		internal static bool TryScanText(this SigScanner scanner, string sig, out IntPtr result)
		{
			result = IntPtr.Zero;
			try
			{
				result = scanner.ScanText(sig);
				return true;
			}
			catch (KeyNotFoundException)
			{
				return false;
			}
		}

		private static unsafe byte[] ReadTerminatedBytes(byte* ptr)
		{
			if (ptr == null)
			{
				return new byte[0];
			}

			var bytes = new List<byte>();
			while (*ptr != 0)
			{
				bytes.Add(*ptr);
				ptr += 1;
			}

			return bytes.ToArray();
		}

		internal static unsafe string ReadTerminatedString(byte* ptr)
		{
			return Encoding.UTF8.GetString(ReadTerminatedBytes(ptr));
		}

		internal static bool ContainsIgnoreCase(this string haystack, string needle)
		{
			return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase) >= 0;
		}

		//internal static bool? ToNBool(this int s)
		//{
		//	if (s == 0)
		//	{
		//		return null;
		//	}
		//	else
		//	{
		//		if (s == 1)
		//		{
		//			return true;
		//		}

		//		if (s == 2)
		//		{
		//			return false;
		//		}
		//	}

		//	throw new ArgumentOutOfRangeException();
		//}


		public static uint SetAlpha(this uint color32, uint alpha) => (color32 << 8 >> 8) + (alpha << 24);
		public static uint Invert(this uint color32) => ((0xFFFF_FFFF - (color32 << 8)) >> 8) + (color32 >> 24 << 24);
		public static Vector2 Normalize(this Vector2 v)
		{
			float a = v.Length();
			if (!MathUtil.IsZero(a))
			{
				float num = 1f / a;
				v.X *= num;
				v.Y *= num;
				return v;
			}

			return v;
		}


		public static Vector2 RotationToNormalizedVector(float rotation)
		{
			return new Vector2((float)Math.Sin(rotation), (float)Math.Cos(rotation));
		}
		public static Vector2 Zoom(this Vector2 vin, float zoom, Vector2 origin)
		{
			return origin + (vin - origin) * zoom;
		}

		public static Vector2 Rotate(this Vector2 vin, float rotation, Vector2 origin)
		{
			return origin + (vin - origin).Rotate(rotation);
		}
		public static Vector2 Rotate(this Vector2 vin, float rotation)
		{
			return vin.Rotate(new Vector2((float)Math.Sin(rotation), (float)Math.Cos(rotation)));
		}
		public static Vector2 Rotate(this Vector2 vin, Vector2 rotation, Vector2 origin)
		{
			return origin + (vin - origin).Rotate(rotation);
		}
		public static Vector2 Rotate(this Vector2 vin, Vector2 rotation)
		{
			rotation = rotation.Normalize();
			return new(rotation.Y * vin.X + rotation.X * vin.Y, rotation.Y * vin.Y - rotation.X * vin.X);
		}

		public static float ToArc(this Vector2 vin)
		{
			return (float)Math.Sin(vin.X);
		}

		public static void MassTranspose(Vector2[] vin, Vector2 pivot, Vector2 rotation)
		{
			for (int i = 0; i < vin.Length; i++)
			{
				vin[i] = (vin[i] - pivot).Rotate(rotation) + pivot;
			}
		}
		public static void MassTranspose(Vector2[] vin, Vector2 pivot, float rotation)
		{
			for (int i = 0; i < vin.Length; i++)
			{
				vin[i] = (vin[i] - pivot).Rotate(rotation) + pivot;
			}
		}

		public static Vector2 ToNormalizedVector2(this float rad)
		{
			return new Vector2((float)Math.Sin(rad), (float)Math.Cos(rad));
		}

		public static Vector3 ToVector3(this Vector2 vin)
		{
			return new Vector3(vin.X, 0, vin.Y);
		}


		public static T[] ReadArray<T>(IntPtr pointer, int length) where T : struct
		{
			var size = Marshal.SizeOf(typeof(T));
			var managedArray = new T[length];

			for (int i = 0; i < length; i++)
			{
				IntPtr ins = new IntPtr(pointer.ToInt64() + i * size);
				managedArray[i] = Marshal.PtrToStructure<T>(ins);
			}

			return managedArray;
		}

		public static unsafe T[] ReadArrayUnmanaged<T>(IntPtr pointer, int length) where T : unmanaged
		{
			var managedArray = new T[length];

			for (int i = 0; i < length; i++)
			{
				// ReSharper disable once PossibleNullReferenceException
				managedArray[i] = ((T*)pointer)[i];
			}

			return managedArray;
		}

		public static void Log(this object o) => PluginLog.Information((o is IntPtr i ? i.ToInt64().ToString("X") : o.ToString()));
		public static void Log(this object o, string prefix) => PluginLog.Information($"{prefix}: {(o is IntPtr i ? i.ToInt64().ToString("X") : o.ToString())}");





		//public static string BytesToBase64String(byte[] data)
		//{
		//	var d = Compress(data);
		//	return System.Convert.ToBase64String(d);
		//}
		//public static byte[] Base64StringToBytes(string base64string)
		//{
		//	var data = System.Convert.FromBase64String(base64string);
		//	return Decompress(data);

		//}
		public static string ToCompressedString<T>(this T obj)
		{
			return Compress(obj.ToJsonString());
		}

		public static T DecompressStringToObject<T>(this string compressedString)
		{
			return JsonStringToObject<T>(Decompress(compressedString));
		}

		public static string ToJsonString(this object obj)
		{
			return JsonConvert.SerializeObject(obj);
		}
		public static T JsonStringToObject<T>(this string str)
		{
			return JsonConvert.DeserializeObject<T>(str);
		}

		public static byte[] GetSHA1(string s)
		{
			var bytes = Encoding.Unicode.GetBytes(s);
			return SHA1.Create().ComputeHash(bytes);
		}

		public static string Compress(string s)
		{
			var bytes = Encoding.Unicode.GetBytes(s);
			using (var msi = new MemoryStream(bytes))
			using (var mso = new MemoryStream())
			{
				using (var gs = new GZipStream(mso, CompressionLevel.Optimal))
				{
					msi.CopyTo(gs);
				}
				return System.Convert.ToBase64String(mso.ToArray());
			}
		}

		public static string Decompress(string s)
		{
			var bytes = System.Convert.FromBase64String(s);
			using (var msi = new MemoryStream(bytes))
			using (var mso = new MemoryStream())
			{
				using (var gs = new GZipStream(msi, CompressionMode.Decompress))
				{
					gs.CopyTo(mso);
				}
				return Encoding.Unicode.GetString(mso.ToArray());
			}
		}

		//public static byte[] Compress(byte[] data)
		//{
		//	MemoryStream output = new MemoryStream();
		//	using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
		//	{
		//		dstream.Write(data, 0, data.Length);
		//	}
		//	return output.ToArray();
		//}

		//public static byte[] Decompress(byte[] data)
		//{
		//	MemoryStream input = new MemoryStream(data);
		//	MemoryStream output = new MemoryStream();
		//	using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
		//	{
		//		dstream.CopyTo(output);
		//	}
		//	return output.ToArray();
		//}
	}
}
