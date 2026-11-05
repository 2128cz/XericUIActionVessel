using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 导出：Unity 的图像类型（Texture2D / Sprite / RenderTexture）落成资产，
	/// 或按 Texture2D 自带的编码方法写成 PNG / JPG / TGA / EXR 文件。
	///
	/// <para><b>文件落在工程内和工程外走两条路</b>：落在 Assets 下面时必须触发导入并写导入设置
	/// （sRGB 与是否 Sprite），否则 Unity 会按默认值重新解释这张图——烘的时候是 sRGB、
	/// 进引擎变成线性，是最难查的那种"颜色不对"。</para>
	/// </summary>
	internal sealed class ShaderBakeExporter
	{
		private const string kPrefLastDir = "XericUI.ShaderBake.LastDir";

		private readonly ShaderBakeRenderer m_Renderer;
		private readonly Action<string> m_Report;

		public ShaderBakeExporter(ShaderBakeRenderer renderer, Action<string> report)
		{
			m_Renderer = renderer;
			m_Report = report;
		}

		/// <summary> 把导出项灌进一个 GenericMenu（工具栏那颗按钮点开来的是它） </summary>
		public void BuildMenu(GenericMenu menu, string suggestedName)
		{
			string name = SanitizeName(suggestedName);

			menu.AddItem(new GUIContent("PNG 文件…"), false, () => SaveFile(ShaderBakeImageFormat.Png, name));
			menu.AddItem(new GUIContent("JPG 文件…"), false, () => SaveFile(ShaderBakeImageFormat.Jpg, name));
			menu.AddItem(new GUIContent("TGA 文件…"), false, () => SaveFile(ShaderBakeImageFormat.Tga, name));
			menu.AddItem(new GUIContent("EXR 文件…"), false, () => SaveFile(ShaderBakeImageFormat.Exr, name));

			menu.AddSeparator(string.Empty);
			menu.AddItem(new GUIContent("Texture2D 资产（.asset）…"), false, () => SaveTextureAsset(name));
			menu.AddItem(new GUIContent("Sprite 资产（png + 导入设置）…"), false, () => SaveSpriteAsset(name));
			menu.AddItem(new GUIContent("RenderTexture 资产（.asset）…"), false, () => SaveRenderTextureAsset(name));

			menu.AddSeparator(string.Empty);
			menu.AddItem(new GUIContent("复制烘焙参数（JSON）"), false, CopySettingsJson);
			menu.AddItem(new GUIContent("用参数 JSON 替换…"), false, PasteSettingsJson);
		}

		// ── 文件 ────────────────────────────────────────────────

		public void SaveFile(ShaderBakeImageFormat format, string suggestedName)
		{
			Texture2D texture = m_Renderer.RenderToTexture2D(false);
			if (texture == null)
			{
				Fail(m_Renderer.lastError);
				return;
			}

			byte[] bytes;
			try
			{
				bytes = format.Encode(texture, m_Renderer.settings.output.clearColor);
			}
			finally
			{
				ShaderBakeRenderer.ReleaseTempObject(texture, true);
			}
			if (bytes == null || bytes.Length == 0)
			{
				Fail(format + " 编码失败（这个纹理格式 Unity 的编码器不吃）");
				return;
			}

			string path = EditorUtility.SaveFilePanel(
				"导出 " + format.GetExtension().ToUpperInvariant(), LastDir(), suggestedName, format.GetExtension());
			if (string.IsNullOrEmpty(path))
				return;

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllBytes(path, bytes);
			}
			catch (Exception e)
			{
				Fail("写入失败：" + e.Message);
				return;
			}

			RememberDir(path);
			string assetPath = ToAssetPath(path);
			if (assetPath != null)
			{
				ImportAndConfigure(assetPath, false);
				Report("已导出 " + assetPath);
			}
			else
			{
				Report("已导出 " + path);
			}
		}

		// ── 资产 ────────────────────────────────────────────────

		public void SaveTextureAsset(string suggestedName)
		{
			Texture2D texture = m_Renderer.RenderToTexture2D(false);
			if (texture == null)
			{
				Fail(m_Renderer.lastError);
				return;
			}
			string path = EditorUtility.SaveFilePanelInProject("保存 Texture2D 资产", suggestedName, "asset",
				"这张图会作为可读的 Texture2D 存成 .asset（不走向器，随时可读回像素）");
			if (string.IsNullOrEmpty(path))
			{
				ShaderBakeRenderer.ReleaseTempObject(texture, true);
				return;
			}

			texture.name = Path.GetFileNameWithoutExtension(path);
			// CreateAsset 不吃 DontSave / hideFlags 非空的对象，落盘前必须摘掉
			texture.hideFlags = HideFlags.None;
			AssetDatabase.CreateAsset(texture, path);
			AssetDatabase.SaveAssets();
			RememberDir(path);
			Report("已保存 " + path);
		}

		/// <summary>
		/// Sprite 走"写 png + 设导入器"这条路，而不是把 Sprite 对象本身存成资产：
		/// Sprite.Create 出来的是一张纹理上的临时对象，直接 CreateAsset 会被当成非法资产。
		/// 让 Unity 自己按 Sprite 类型导入，拿到的 Sprite 才是别处也能用的正常资产。
		/// </summary>
		public void SaveSpriteAsset(string suggestedName)
		{
			string path = EditorUtility.SaveFilePanelInProject("保存 Sprite 资产", suggestedName, "png",
				"先写成 png，再把导入类型设为 Sprite（Single）");
			if (string.IsNullOrEmpty(path))
				return;

			Texture2D texture = m_Renderer.RenderToTexture2D(false);
			if (texture == null)
			{
				Fail(m_Renderer.lastError);
				return;
			}
			byte[] bytes;
			try
			{
				bytes = ShaderBakeImageFormat.Png.Encode(texture, m_Renderer.settings.output.clearColor);
			}
			finally
			{
				ShaderBakeRenderer.ReleaseTempObject(texture, true);
			}
			if (bytes == null)
			{
				Fail("PNG 编码失败");
				return;
			}

			File.WriteAllBytes(path, bytes);
			ImportAndConfigure(path, true);
			RememberDir(path);
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			Report(sprite != null ? "已保存 Sprite " + path : "已写入 " + path + "（没拿到 Sprite，检查导入设置）");
		}

		public void SaveRenderTextureAsset(string suggestedName)
		{
			string path = EditorUtility.SaveFilePanelInProject("保存 RenderTexture 资产", suggestedName, "asset",
				"RT 资产可以反复往里渲，也能被材质直接引用");
			if (string.IsNullOrEmpty(path))
				return;

			// 存成资产要能被别处直接采样/读回，单采样即可；采样样式由烘焙设置里的 MSAA 只在预览时体现
			var rt = m_Renderer.settings.output.CreateRenderTexture(Path.GetFileNameWithoutExtension(path), 1);
			if (m_Renderer.Render(rt) == null)
			{
				Fail(m_Renderer.lastError);
				ShaderBakeRenderer.ReleaseTempObject(rt, true);
				return;
			}
			rt.hideFlags = HideFlags.None;
			AssetDatabase.CreateAsset(rt, path);
			AssetDatabase.SaveAssets();
			RememberDir(path);
			Report("已保存 " + path);
		}

		// ── 参数搬运 ────────────────────────────────────────────

		private void CopySettingsJson()
		{
			EditorGUIUtility.systemCopyBuffer = m_Renderer.settings.ToJson();
			Report("烘焙参数已复制到剪贴板");
		}

		private void PasteSettingsJson()
		{
			string json = EditorGUIUtility.systemCopyBuffer;
			if (string.IsNullOrEmpty(json) || json.IndexOf("output", StringComparison.Ordinal) < 0)
			{
				Fail("剪贴板里不像是烘焙参数 JSON");
				return;
			}
			ShaderBakeSettings parsed = ShaderBakeSettings.FromJson(json);
			m_Renderer.settings.output = parsed.output;
			m_Renderer.settings.surface = parsed.surface;
			m_Renderer.settings.camera = parsed.camera;
			Report("烘焙参数已从剪贴板替换");
			Action apply = settingsReplaced;
			if (apply != null)
				apply.Invoke();
		}

		/// <summary> 参数被外部整体换掉之后，窗口要把界面数值同步回来 </summary>
		public event Action settingsReplaced;

		// ── 杂项 ────────────────────────────────────────────────

		/// <summary>
		/// 落在 Assets 下面时触发导入并设好类型。<paramref name="asSprite"/> 为真时按 Sprite 导入；
		/// 否则按普通贴图，并把 sRGB 跟烘焙设置对齐。
		/// </summary>
		private void ImportAndConfigure(string assetPath, bool asSprite)
		{
			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
			var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
			if (importer == null)
				return;

			importer.textureType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
			if (asSprite)
				importer.spriteImportMode = SpriteImportMode.Single;
			importer.sRGBTexture = m_Renderer.settings.output.sRGB && !m_Renderer.settings.output.hdr;
			importer.mipmapEnabled = false;
			// 浮点位深交给导入器按源文件判断（EXR 源自己带格式），这里不去设 per-platform 压缩格式
			importer.SaveAndReimport();
		}

		/// <summary> 工程内的绝对路径换成 Assets/... 相对路径；不在工程内返回 null </summary>
		private static string ToAssetPath(string absolutePath)
		{
			string normalized = absolutePath.Replace('\\', '/');
			string dataPath = Application.dataPath.Replace('\\', '/');
			if (!normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
				return null;
			return "Assets" + normalized.Substring(dataPath.Length);
		}

		/// <summary> 文件名直接拿去当路径用，分隔符和非法字符要先剔掉 </summary>
		private static string SanitizeName(string suggestedName)
		{
			if (string.IsNullOrEmpty(suggestedName))
				return "BakedImage";
			foreach (char invalid in Path.GetInvalidFileNameChars())
				suggestedName = suggestedName.Replace(invalid, '_');
			return suggestedName.Replace('/', '_').Replace('\\', '_');
		}

		private static string LastDir()
		{
			string dir = EditorPrefs.GetString(kPrefLastDir, string.Empty);
			return string.IsNullOrEmpty(dir) || !Directory.Exists(dir) ? Application.dataPath : dir;
		}

		private static void RememberDir(string path)
		{
			try
			{
				string dir = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(dir))
					EditorPrefs.SetString(kPrefLastDir, dir);
			}
			catch (Exception)
			{
				// 记不住上次的目录不是错误，下次弹默认目录而已
			}
		}

		private void Report(string message)
		{
			if (m_Report != null) m_Report(message);
			Debug.Log("[ShaderBake] " + message);
		}

		private void Fail(string message)
		{
			if (m_Report != null) m_Report("导出失败：" + message);
			Debug.LogError("[ShaderBake] " + message);
		}
	}
}
