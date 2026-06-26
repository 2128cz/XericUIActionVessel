using System.Collections.Generic;

using UnityEngine;

namespace XericUI.Core.StyleSheetUI
{
	/// <summary>
	/// 标记可被全局刷新的样式组件。
	/// 实现此接口的组件应在 OnEnable 调用 RegisterForStyleRefresh，
	/// 在 OnDisable 调用 UnregisterForStyleRefresh。
	/// </summary>
	public interface IStyleRefreshable
	{
		void RefreshStyle();
	}

	/// <summary>
	/// 全局样式刷新管理器——维护所有活跃的 IStyleRefreshable 组件。
	/// 当外部样式表文件更新后，调用 ForceRefreshAll() 可一键刷新所有组件。
	/// </summary>
	public static class StyleRefreshManager
	{
		private static readonly HashSet<IStyleRefreshable> s_Components = new HashSet<IStyleRefreshable>();

		/// <summary>注册一个样式组件</summary>
		public static void Register(IStyleRefreshable component)
		{
			if (component != null)
				s_Components.Add(component);
		}

		/// <summary>注销一个样式组件</summary>
		public static void Unregister(IStyleRefreshable component)
		{
			if (component != null)
				s_Components.Remove(component);
		}

		/// <summary>
		/// 强制刷新所有已注册的样式组件。
		/// 先调用 StyleManager.ForceRefresh() 同步样式表数据，
		/// 再遍历所有组件重新读取并应用样式值。
		/// </summary>
		public static void ForceRefreshAll()
		{
#if XERIC_STYLESHEET_ENABLED
			XericLibrary.Runtime.SuperStyleSheet.StyleManager.ForceRefresh();
#endif

			int count = 0;
			foreach (var c in s_Components)
			{
				try { c.RefreshStyle(); } catch { }
				count++;
			}

#if UNITY_EDITOR
			Debug.Log($"[StyleRefresh] 已刷新 {count} 个样式组件。");
#endif
		}

		/// <summary>当前注册的组件数</summary>
		public static int RegisteredCount => s_Components.Count;
	}
}
