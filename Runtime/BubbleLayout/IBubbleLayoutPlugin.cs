using System.Collections.Generic;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 气泡布局插件基础接口，提供执行优先级
	/// </summary>
	public interface IBubbleLayoutPlugin
	{
		/// <summary>
		/// 是否启用此插件，关闭后跳过所有流程
		/// </summary>
		bool Enabled { get; }

		/// <summary>
		/// 执行优先级，值越大越靠后执行
		/// </summary>
		int Order { get; }
	}

	/// <summary>
	/// 气泡动画插件接口 —— 在动画阶段执行，负责添加力、速度等物理量
	/// </summary>
	public interface IBubbleAnimationPlugin : IBubbleLayoutPlugin
	{
		/// <summary>
		/// 处理动画阶段，对每个气泡元素施加力/速度/动量等
		/// </summary>
		/// <param name="items">所有气泡元素数据列表</param>
		/// <param name="deltaTime">帧间隔时间</param>
		void ProcessAnimation(List<BubbleItemData> items, float deltaTime);
	}

	/// <summary>
	/// 气泡约束插件接口 —— 在约束阶段执行，负责限制位置、排列布局等
	/// </summary>
	public interface IBubbleConstraintPlugin : IBubbleLayoutPlugin
	{
		/// <summary>
		/// 处理约束阶段，对气泡元素位置进行限制或排列
		/// </summary>
		/// <param name="items">所有气泡元素数据列表</param>
		/// <param name="layoutCenter">布局中心点</param>
		void ProcessConstraint(List<BubbleItemData> items, UnityEngine.Vector2 layoutCenter);
	}
}
