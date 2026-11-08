using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 可导出的图片文件编码。全部走 Unity 自己的 <c>ImageConversion</c> 扩展方法，不引第三方编码器。
	/// </summary>
	public enum ShaderBakeImageFormat
	{
		/// <summary> PNG：带 alpha，无损 </summary>
		Png,

		/// <summary> JPG：无 alpha，导出前会把透明压到背景色上 </summary>
		Jpg,

		/// <summary> TGA：无压缩、带 alpha，Photoshop / 老管线常用 </summary>
		Tga,

		/// <summary> EXR：浮点 HDR，配 output.hdr 用 </summary>
		Exr
	}

	/// <summary>
	/// 编码分派。注意这几个函数都要求纹理是可读的、且是非压缩格式——
	/// 所以入口只接 <see cref="ShaderBakeReadback"/> 造出来的 RGBA32 / RGBAFloat，别把导入的资源塞进来。
	/// </summary>
	public static class ShaderBakeImageFormatExtensions
	{
		public static string GetExtension(this ShaderBakeImageFormat format)
		{
			switch (format)
			{
				case ShaderBakeImageFormat.Jpg: return "jpg";
				case ShaderBakeImageFormat.Tga: return "tga";
				case ShaderBakeImageFormat.Exr: return "exr";
				default: return "png";
			}
		}

		/// <summary> 这个格式能不能存 alpha </summary>
		public static bool SupportsAlpha(this ShaderBakeImageFormat format)
		{
			return format != ShaderBakeImageFormat.Jpg;
		}

		/// <summary>
		/// 编码成文件字节。<paramref name="flattenBackground"/> 只给 JPG 用：它没有 alpha 通道，
		/// 直接把 RGBA 交上去会得到一圈发黑的边，所以先按这个颜色把透明压平（顺带把浮点截到 0~1）。
		/// </summary>
		public static byte[] Encode(this ShaderBakeImageFormat format, Texture2D texture, Color flattenBackground)
		{
			if (texture == null)
				throw new ArgumentNullException(nameof(texture));

			if (format == ShaderBakeImageFormat.Jpg)
			{
				var flattened = ShaderBakeReadback.FlattenOntoBackground(texture, flattenBackground);
				try
				{
					return flattened.EncodeToJPG();
				}
				finally
				{
					// 编辑器非播放状态下 Object.Destroy 会被拒绝并报错，统一走这条能分辨时机的回收
					ShaderBakeRenderer.ReleaseTempObject(flattened);
				}
			}

			switch (format)
			{
				case ShaderBakeImageFormat.Tga:
					return texture.EncodeToTGA();
				case ShaderBakeImageFormat.Exr:
					return texture.EncodeToEXR(Texture2D.EXRFlags.None);
				default:
					return texture.EncodeToPNG();
			}
		}

		/// <summary>
		/// EXR 要求浮点纹理才能存下超过 1 的分量；非 HDR 的源用 RGBA32 烘，导 EXR 时格式仍合法但值域被截断，
		/// 这里给个提示而不是报错。
		/// </summary>
		public static bool NeedsFloatSource(this ShaderBakeImageFormat format)
		{
			return format == ShaderBakeImageFormat.Exr;
		}
	}
}
