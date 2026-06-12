using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 气泡布局插件基类 —— 提供 Editor 识别标记和 Enabled/Order 公共实现。
	/// 所有插件应继承此类而非直接实现 MonoBehaviour。
	/// </summary>
	public abstract class BubbleLayoutPluginBase : MonoBehaviour, IBubbleLayoutPlugin
	{
		[Tooltip("是否启用此插件")]
		[SerializeField] private bool m_PluginEnabled = true;

		[Tooltip("执行优先级，值越大越靠后执行")]
		[SerializeField] private int m_PluginOrder = 10;

		public bool Enabled => m_PluginEnabled;
		public int Order => m_PluginOrder;

#if UNITY_EDITOR
		/// <summary>
		/// 子类可重写此方法在 Reset 时设置不同的默认 Order
		/// </summary>
		protected virtual int EditorDefaultOrder => 10;

		protected virtual void Reset()
		{
			m_PluginOrder = EditorDefaultOrder;
			m_PluginEnabled = true;
		}
#endif
	}
}
