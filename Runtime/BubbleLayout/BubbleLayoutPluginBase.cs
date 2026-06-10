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
		[SerializeField] protected bool m_Enabled = true;

		[Tooltip("执行优先级，值越大越靠后执行")]
		[SerializeField] protected int m_Order = 10;

		public bool Enabled => m_Enabled;
		public int Order => m_Order;

#if UNITY_EDITOR
		/// <summary>
		/// 子类可重写此方法在 Reset 时设置不同的默认 Order
		/// </summary>
		protected virtual int EditorDefaultOrder => 10;

		protected virtual void Reset()
		{
			m_Order = EditorDefaultOrder;
			m_Enabled = true;
		}
#endif
	}
}
