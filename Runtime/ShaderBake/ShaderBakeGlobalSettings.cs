using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary> 能喂给 shader 的全局量类别，决定用哪个 SetGlobal 入口 </summary>
	public enum ShaderBakeGlobalKind
	{
		Float,
		Vector,
		Color,
		Texture
	}

	/// <summary>
	/// 一条全局参数。贴图引用标了 <see cref="NonSerializedAttribute"/>：JsonUtility 存不下来资产引用，
	/// 硬存一个实例 ID 反而会在换会话后变成半死的引用。
	/// </summary>
	[Serializable]
	public class ShaderBakeGlobalEntry
	{
		public string name;

		public ShaderBakeGlobalKind kind;

		[Tooltip("关掉就完全不动这个全局量，包括不捕获、不还原")]
		public bool enabled;

		public float x;
		public float y;
		public float z;
		public float w;

		[NonSerialized] public Texture texture;

		public ShaderBakeGlobalEntry() { }

		public ShaderBakeGlobalEntry(string name, ShaderBakeGlobalKind kind, bool enabled = false)
		{
			this.name = name;
			this.kind = kind;
			this.enabled = enabled;
			w = 1f;
		}

		public Vector4 ToVector() { return new Vector4(x, y, z, w); }

		public Color ToColor() { return new Color(x, y, z, w); }
	}

	/// <summary>
	/// 一组全局参数。烘焙走的是离屏命令缓冲，没有相机在渲染，"灯在哪、环境光是什么"这类由引擎
	/// 在相机流程里塞进去的全局量天然缺失——这里就是补那一块的口子。
	///
	/// <para><b>只改自己认识的那几条，并且画完就还原</b>：不还原等于把一次烘焙的临时值泄漏给整个编辑器。
	/// 还原是尽力而为：引擎每帧会重设的内置量（例如相机相关的矩阵）可能被覆盖回去。</para>
	/// </summary>
	[Serializable]
	public class ShaderBakeGlobalSettings
	{
		[Tooltip("要参与烘焙的全局参数。没勾选的行完全不参与")]
		public List<ShaderBakeGlobalEntry> entries = new List<ShaderBakeGlobalEntry>();

		/// <summary>
		/// 把启用的条目写进全局，并返回一组还原动作（按相反顺序执行）。
		/// </summary>
		public List<Action> Apply()
		{
			var restores = new List<Action>();
			if (entries == null)
				return restores;

			for (int i = 0; i < entries.Count; i++)
			{
				ShaderBakeGlobalEntry entry = entries[i];
				if (entry == null || !entry.enabled || string.IsNullOrEmpty(entry.name))
					continue;

				string name = entry.name;
				switch (entry.kind)
				{
					case ShaderBakeGlobalKind.Float:
					{
						float previous = Shader.GetGlobalFloat(name);
						Shader.SetGlobalFloat(name, entry.x);
						restores.Add(() => Shader.SetGlobalFloat(name, previous));
						break;
					}
					case ShaderBakeGlobalKind.Vector:
					{
						Vector4 previous = Shader.GetGlobalVector(name);
						Shader.SetGlobalVector(name, entry.ToVector());
						restores.Add(() => Shader.SetGlobalVector(name, previous));
						break;
					}
					case ShaderBakeGlobalKind.Color:
					{
						Color previous = Shader.GetGlobalColor(name);
						Shader.SetGlobalColor(name, entry.ToColor());
						restores.Add(() => Shader.SetGlobalColor(name, previous));
						break;
					}
					case ShaderBakeGlobalKind.Texture:
					{
						Texture previous = Shader.GetGlobalTexture(name);
						Shader.SetGlobalTexture(name, entry.texture);
						restores.Add(() => Shader.SetGlobalTexture(name, previous));
						break;
					}
				}
			}
			return restores;
		}

		/// <summary> 按名字找已有条目，没有就追加一条（扫描与预设都走这里，避免重复） </summary>
		public ShaderBakeGlobalEntry Ensure(string name, ShaderBakeGlobalKind kind)
		{
			if (entries == null)
				entries = new List<ShaderBakeGlobalEntry>();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i] != null && entries[i].name == name)
					return entries[i];
			}
			var entry = new ShaderBakeGlobalEntry(name, kind);
			entries.Add(entry);
			return entry;
		}

		/// <summary>
		/// 常用光照/环境量。全部默认关闭：不同渲染管线对这些名字的语义不完全一样，
		/// 该不该开由用户决定，工具不替他改画面。
		/// </summary>
		public void AddEnvironmentPresets()
		{
			AddVector("_WorldSpaceLightPos0", 0.3f, 0.7f, -0.6f, 0f);
			AddColor("_LightColor0", 1f, 1f, 1f, 1f);
			AddColor("unity_AmbientSky", 0.21f, 0.23f, 0.25f, 1f);
			AddColor("unity_AmbientEquator", 0.11f, 0.12f, 0.13f, 1f);
			AddColor("unity_AmbientGround", 0.04f, 0.04f, 0.04f, 1f);
			AddVector("_Time", 0f, 0f, 0f, 0f);
			Ensure("unity_SpecCube0", ShaderBakeGlobalKind.Texture);
		}

		private void AddVector(string name, float x, float y, float z, float w)
		{
			ShaderBakeGlobalEntry entry = Ensure(name, ShaderBakeGlobalKind.Vector);
			entry.x = x; entry.y = y; entry.z = z; entry.w = w;
		}

		private void AddColor(string name, float x, float y, float z, float w)
		{
			ShaderBakeGlobalEntry entry = Ensure(name, ShaderBakeGlobalKind.Color);
			entry.x = x; entry.y = y; entry.z = z; entry.w = w;
		}
	}
}
