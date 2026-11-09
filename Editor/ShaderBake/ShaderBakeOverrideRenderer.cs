using UnityEngine;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 用一张"覆盖材质"按<b>同一套</b>面片 / 相机再渲一次的探针。
	///
	/// <para>两处用得上，问的是同一个问题的两面：</para>
	/// <list type="bullet">
	/// <item><description>管线自检 —— 内置纯色 shader，回答"画不出来该怪管线还是怪被测 shader"</description></item>
	/// <item><description>测试视图 —— UV / 梯度 / 棋盘，回答"这一格占画面多大、铺了哪段 UV"</description></item>
	/// </list>
	///
	/// <para><b>settings 是共享引用，不是副本</b>：探针必须和被测对象同参数，否则比较没有意义。
	/// 材质<b>不归这里所有</b>，由调用方建、由调用方销毁——测试视图那张要设 _Mode，会反复用。</para>
	///
	/// <para>探针是<b>常驻</b>的：RT 与网格按签名缓存，持续刷新（实时预览开着时每秒几十次）如果
	/// 每次都重建一遍探针，卡顿就来自这里。</para>
	/// </summary>
	internal sealed class ShaderBakeOverrideRenderer
	{
		private ShaderBakeRenderer m_Probe;

		/// <summary>
		/// 探针自己那份网格。参数是共享的，所以顶点数与真实渲染一致——但真实渲染那次未必已经建过网格，
		/// 测试视图的读数得能从探针这边拿到，否则状态栏的顶点数会空着。
		/// </summary>
		public Mesh geometry
		{
			get { return m_Probe != null ? m_Probe.geometry : null; }
		}

		/// <summary>
		/// 用给定材质按同一套参数重画一次。失败返回 null 并把原因写进 <paramref name="error"/>。
		/// </summary>
		public RenderTexture RenderWithMaterial(ShaderBakeSettings settings, Material material, out string error)
		{
			error = string.Empty;
			if (settings == null || material == null)
			{
				error = material == null ? "探针材质还没建起来" : "没有参数可渲";
				return null;
			}

			if (m_Probe == null)
				m_Probe = new ShaderBakeRenderer { settings = settings };
			m_Probe.material = material;

			// 探针 shader 只有一个 pass，用户填的 pass 下标是给被测 shader 的，照抄过来只会画成空画面，
			// 于是"pass 选错"被误诊成"管线坏了"。这里只借 settings 用一次，出来就还原。
			int subjectPass = settings.output.shaderPass;
			settings.output.shaderPass = 0;
			RenderTexture rt;
			try
			{
				rt = m_Probe.Render();
				if (rt == null)
					error = m_Probe.lastError;
			}
			finally
			{
				settings.output.shaderPass = subjectPass;
			}
			return rt;
		}

		/// <summary> 收掉探针，连带它那张 RT 与网格。关窗口时调用 </summary>
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
