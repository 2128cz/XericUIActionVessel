using UnityEngine;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 用一张"覆盖材质"按<b>同一套</b>面片 / 相机 / pass 再渲一次的探针。
	///
	/// <para>两处用得上，问的是同一个问题的两面：</para>
	/// <list type="bullet">
	/// <item><description>管线自检 —— 内置纯色 shader，回答"画不出来该怪管线还是怪被测 shader"</description></item>
	/// <item><description>UV 检查 —— 把顶点 UV 本身画成颜色，回答"这一格到底铺了哪段 UV"</description></item>
	/// </list>
	///
	/// <para><b>settings 是共享引用，不是副本</b>：探针必须和被测对象同参数，否则比较没有意义。
	/// 材质只在渲染那一刻需要，画完即可销毁（画面已经在 RT 里，留着只会让人误以为它还在影响显示）；
	/// RT 由探针自己持有，所以探针要活到下一次正常渲染，那张图才还在。</para>
	/// </summary>
	internal sealed class ShaderBakeOverrideRenderer
	{
		private ShaderBakeRenderer m_Probe;

		/// <summary>
		/// 探针自己那份网格。参数是共享的，所以顶点数与真实渲染一致——但真实渲染那次未必已经建过网格，
		/// UV 视图的读数得能从探针这边拿到，否则状态栏的顶点数会空着。
		/// </summary>
		public Mesh geometry
		{
			get { return m_Probe != null ? m_Probe.geometry : null; }
		}

		/// <summary>
		/// 换一张 shader 重画一次。失败返回 null 并把原因写进 <paramref name="error"/>，
		/// 成功时旧探针已经换新，之前那张图随之作废。
		/// </summary>
		public RenderTexture Render(ShaderBakeSettings settings, Shader shader, string shaderName, out string error)
		{
			error = string.Empty;
			if (shader == null)
			{
				Release();
				error = "找不到 " + shaderName;
				return null;
			}
			var material = new Material(shader)
			{
				name = "ShaderBakeOverride",
				hideFlags = HideFlags.DontSave
			};
			RenderTexture rt = RenderWithMaterial(settings, material, out error);
			// 材质用完即弃：画面已经在 RT 里，留着只会让人误以为它还在影响显示
			ShaderBakeRenderer.ReleaseTempObject(material, true);
			return rt;
		}

		/// <summary>
		/// 用调用方给的那张材质重画一次。材质<b>不归这里所有</b>，画完由调用方销毁——
		/// 自检那张纯色材质要先设颜色，只能由调用方建。
		/// </summary>
		public RenderTexture RenderWithMaterial(ShaderBakeSettings settings, Material material, out string error)
		{
			error = string.Empty;
			Release();

			if (settings == null)
			{
				error = "没有参数可渲";
				return null;
			}

			// 探针这两张 shader 都只有一个 pass。用户填的 pass 下标是给被测 shader 的，原样传过来
			// 只会让探针画成空画面，于是"pass 选错"被误诊成"管线坏了"。
			// 这里只借 settings 用一次，出来就还原，不会把改动留在共享对象上。
			int subjectPass = settings.output.shaderPass;
			settings.output.shaderPass = 0;
			RenderTexture rt;
			try
			{
				m_Probe = new ShaderBakeRenderer { settings = settings, material = material };
				rt = m_Probe.Render();
				if (rt == null)
					error = m_Probe.lastError;
			}
			finally
			{
				settings.output.shaderPass = subjectPass;
			}

			if (rt == null)
			{
				Release();
				return null;
			}
			return rt;
		}

		/// <summary> 收掉探针，连带它那张 RT。下一次真实渲染结果回来时调用 </summary>
		public void Release()
		{
			if (m_Probe == null)
				return;
			// 探针自己回收那张 RT，所以只走它一条路，别两边各销毁一次
			m_Probe.Dispose(true);
			m_Probe = null;
		}
	}
}
