using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 一次性调用的门面：给"我就是要把这个 shader 烘成一张图/一个文件"的运行时代码用。
	/// 需要在帧之间反复改参数重画（比如编辑器预览）请直接用 <see cref="ShaderBakeRenderer"/>，
	/// 它会把目标纹理和网格留着复用。
	/// </summary>
	public static class ShaderBake
	{
		/// <summary>
		/// 渲进一张新建的 RenderTexture。<b>返回的纹理由调用方销毁</b>（用
		/// <see cref="ShaderBakeRenderer.ReleaseTempObject"/> 就行）。失败返回 null，原因写在 lastError。
		/// </summary>
		public static RenderTexture BakeToRenderTexture(Material material, ShaderBakeSettings settings, out string lastError)
		{
			lastError = null;
			using (var renderer = CreateRenderer(material, settings, out lastError))
			{
				if (renderer == null)
					return null;
				RenderTexture rt = renderer.Render();
				lastError = renderer.lastError;
				if (rt == null)
					return null;

				// renderer.Dispose 会把内部那张一起销毁，所以交出去的是按同一套参数另建的一张。
				// 这张要能被 Blit 写入，只能是单采样（采样已在 Render 里解析过了）。
				var effective = settings != null ? settings : ShaderBakeSettings.CreateDefault();
				var copy = effective.output.CreateRenderTexture("ShaderBakeResult", 1);
				Graphics.Blit(rt, copy);
				return copy;
			}
		}

		/// <summary> 渲成可读 Texture2D。调用方负责销毁。 </summary>
		public static Texture2D BakeToTexture2D(Material material, ShaderBakeSettings settings, bool generateMips = false)
		{
			string lastError;
			using (var renderer = CreateRenderer(material, settings, out lastError))
			{
				if (renderer == null)
				{
					Debug.LogError("[ShaderBake] " + lastError);
					return null;
				}
				return renderer.RenderToTexture2D(generateMips);
			}
		}

		/// <summary> 渲成 Sprite（连同它持有的纹理一起交给调用方）。 </summary>
		public static Sprite BakeToSprite(Material material, ShaderBakeSettings settings, float pixelsPerUnit = 100f)
		{
			string lastError;
			using (var renderer = CreateRenderer(material, settings, out lastError))
			{
				if (renderer == null)
				{
					Debug.LogError("[ShaderBake] " + lastError);
					return null;
				}
				return renderer.RenderToSprite(pixelsPerUnit);
			}
		}

		/// <summary>
		/// 渲成图片文件字节（PNG / JPG / TGA / EXR）。
		/// <paramref name="settings"/> 为 null 时用默认 512×512 那套。
		/// </summary>
		public static byte[] BakeToBytes(Material material, ShaderBakeImageFormat format, ShaderBakeSettings settings = null)
		{
			string lastError;
			using (var renderer = CreateRenderer(material, settings, out lastError))
			{
				if (renderer == null)
				{
					Debug.LogError("[ShaderBake] " + lastError);
					return null;
				}
				Texture2D keep;
				byte[] bytes = renderer.RenderToBytes(format, out keep);
				if (keep != null)
					ShaderBakeRenderer.ReleaseTempObject(keep);
				return bytes;
			}
		}

		/// <summary> 从 Shader 直接烘一份（内部建临时材质并在用完后销毁） </summary>
		public static byte[] BakeToBytes(Shader shader, ShaderBakeImageFormat format, ShaderBakeSettings settings = null)
		{
			if (shader == null)
			{
				Debug.LogError("[ShaderBake] shader 为空");
				return null;
			}
			var temp = new Material(shader) { hideFlags = HideFlags.DontSave };
			try
			{
				return BakeToBytes(temp, format, settings);
			}
			finally
			{
				ShaderBakeRenderer.ReleaseTempObject(temp);
			}
		}

		/// <summary> 把参数与材质装进一个新渲染器；材质为空时返回 null 并写好 lastError </summary>
		public static ShaderBakeRenderer CreateRenderer(Material material, ShaderBakeSettings settings, out string lastError)
		{
			if (material == null)
			{
				lastError = "没有材质";
				return null;
			}
			lastError = string.Empty;
			return new ShaderBakeRenderer
			{
				material = material,
				settings = settings != null ? settings : ShaderBakeSettings.CreateDefault()
			};
		}
	}
}
