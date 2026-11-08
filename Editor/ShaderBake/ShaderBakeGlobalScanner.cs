using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using XericUI.ShaderBake;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 从 shader 源文件里找"不是材质属性、但会被 shader 读到"的全局量。
	///
	/// <para><b>为什么只能做到这个程度</b>：Unity 没有"列出 shader 实际引用了哪些 uniform"的 API，
	/// 编译器把 include 里的东西全展开了。这里做的是文本级扫描——只看 shader 文件里自己声明的
	/// uniform，够覆盖"手写一个 _GlobalTint 却没做成 property"这类常见情况；引擎内置量
	/// （光照方向、环境光、反射探针）靠"常用预设"补齐，不假装能自动枚举。</para>
	/// </summary>
	internal static class ShaderBakeGlobalScanner
	{
		private static readonly Regex kDeclaration = new Regex(
			@"(?m)^[ \t]*(?:uniform[ \t]+)?(float|float2|float3|float4|half|half2|half3|half4|fixed|fixed2|fixed3|fixed4|int|uint|color|sampler2D|sampler3D|samplerCUBE|sampler2DArray|Texture2D|Texture3D|TextureCube|Texture2DArray)[ \t]+([A-Za-z_][A-Za-z0-9_]*)[ \t]*(?:\[[^\]]*\])?[ \t]*;",
			RegexOptions.Compiled);

		/// <summary> 扫描并把新发现的全局量加进参数表（默认关闭）。返回新增条数。 </summary>
		public static int Scan(Shader shader, ShaderBakeGlobalSettings settings, out string note)
		{
			if (shader == null)
			{
				note = "没有 shader";
				return 0;
			}
			string path = AssetDatabase.GetAssetPath(shader);
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				note = "这个 shader 没有源文件（内置或运行时创建），只能手动加常用预设";
				return 0;
			}

			string source;
			try
			{
				source = File.ReadAllText(path);
			}
			catch (IOException e)
			{
				note = "读 shader 源文件失败：" + e.Message;
				return 0;
			}

			var properties = new HashSet<string>();
			for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
				properties.Add(ShaderUtil.GetPropertyName(shader, i));

			int added = 0;
			foreach (Match match in kDeclaration.Matches(source))
			{
				string name = match.Groups[2].Value;
				if (properties.Contains(name) || name.StartsWith("unity_", System.StringComparison.Ordinal))
					continue;
				if (settings.entries != null && settings.entries.Exists(entry => entry != null && entry.name == name))
					continue;
				settings.Ensure(name, KindOf(match.Groups[1].Value));
				added++;
			}
			note = added > 0 ? "扫描到 " + added + " 个全局量（默认关闭）" : "没有扫到新的全局量";
			return added;
		}

		private static ShaderBakeGlobalKind KindOf(string type)
		{
			switch (type)
			{
				case "float":
				case "half":
				case "fixed":
				case "int":
				case "uint":
					return ShaderBakeGlobalKind.Float;
				case "color":
					return ShaderBakeGlobalKind.Color;
				case "sampler2D":
				case "sampler3D":
				case "samplerCUBE":
				case "sampler2DArray":
				case "Texture2D":
				case "Texture3D":
				case "TextureCube":
				case "Texture2DArray":
					return ShaderBakeGlobalKind.Texture;
				default:
					return ShaderBakeGlobalKind.Vector;
			}
		}
	}
}
