using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 一次烘焙所需的全部参数：输出 / 面片 / 离屏相机三组。
	/// 材质不进这里——材质是"内容"，会换、会被就地编辑，而这一份是"怎么渲"，可以存下来复用。
	/// </summary>
	[Serializable]
	public class ShaderBakeSettings
	{
		[Tooltip("输出目标")]
		public ShaderBakeOutputSettings output = new ShaderBakeOutputSettings();

		[Tooltip("被渲染的面片")]
		public ShaderBakeSurfaceSettings surface = new ShaderBakeSurfaceSettings();

		[Tooltip("离屏相机")]
		public ShaderBakeCameraSettings camera = new ShaderBakeCameraSettings();

		[Tooltip("渲染前注入的全局参数（光照方向、环境光、反射贴图等）")]
		public ShaderBakeGlobalSettings globals = new ShaderBakeGlobalSettings();

		/// <summary> 画面宽高比 </summary>
		public float aspect
		{
			get
			{
				if (output.height <= 0 || output.width <= 0) return 1f;
				return (float)output.width / output.height;
			}
		}

		/// <summary> 三组参数各自归位 </summary>
		public void ClampAll()
		{
			output.Clamp();
			surface.Clamp();
			camera.Clamp();
		}

		/// <summary> 复制一份（编辑器窗口拿它当工作副本，改坏了不至于污染存下来的那份） </summary>
		public ShaderBakeSettings Clone()
		{
			return FromJson(ToJson());
		}

		/// <summary> JsonUtility 只吃 [Serializable] 的类，这里三组都满足；Mesh / 材质引用序列化成实例ID，读回来时由界面重新指派 </summary>
		public string ToJson()
		{
			return JsonUtility.ToJson(this);
		}

		public static ShaderBakeSettings FromJson(string json)
		{
			if (string.IsNullOrEmpty(json)) return CreateDefault();
			var s = new ShaderBakeSettings();
			JsonUtility.FromJsonOverwrite(json, s);
			s.ClampAll();
			return s;
		}

		public static ShaderBakeSettings CreateDefault()
		{
			return new ShaderBakeSettings();
		}
	}
}
