using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 2D 物理力解算器 —— 使用半隐式欧拉积分进行速度/位置更新
	/// </summary>
	public static class BubblePhysicsSolver
	{
		/// <summary>
		/// 对气泡元素列表执行一步物理积分，使用每元素自身的 mass 值
		/// </summary>
		/// <param name="items">气泡元素列表</param>
		/// <param name="deltaTime">时间步长</param>
		/// <param name="damping">速度阻尼系数 (0=无阻尼, 1=完全停止)</param>
		public static void Integrate(List<BubbleItemData> items, float deltaTime, float damping = 0.9f)
		{
			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];

				// 保存上一帧状态
				item.prevVelocity = item.velocity;
				item.prevForce = item.force;

				// 半隐式欧拉积分: 力 → 加速度 → 速度 → 位置
				float itemMass = Mathf.Max(item.mass, 0.001f);
				Vector2 acceleration = item.force / itemMass;

				// v_new = v_old * damping + a * dt
				item.velocity = item.velocity * (1f - damping) + acceleration * deltaTime;

				// 动量 = m * v
				item.momentum = item.velocity * itemMass;

				// p_new = p_old + v_new * dt
				item.position += item.velocity * deltaTime;

				// 角动量积分
				item.rotation += item.angularMomentum * deltaTime;

				// 清除本帧力（力只持续一帧，需要每帧重新施加）
				item.force = Vector2.zero;
			}
		}

		/// <summary>
		/// 计算两个位置之间的排斥力（用于气泡排挤）
		/// </summary>
		/// <param name="posA">元素A中心位置</param>
		/// <param name="sizeA">元素A尺寸</param>
		/// <param name="posB">元素B中心位置</param>
		/// <param name="sizeB">元素B尺寸</param>
		/// <param name="stiffness">排斥刚度</param>
		/// <returns>施加在A上的排斥力</returns>
		public static Vector2 CalculateRepulsionForce(
			Vector2 posA, Vector2 sizeA,
			Vector2 posB, Vector2 sizeB,
			float stiffness = 1f)
		{
			Vector2 delta = posA - posB;
			Vector2 halfSizes = (sizeA + sizeB) * 0.5f;

			// 计算X方向重叠量
			float overlapX = halfSizes.x - Mathf.Abs(delta.x);
			// 计算Y方向重叠量
			float overlapY = halfSizes.y - Mathf.Abs(delta.y);

			// 仅在重叠时产生力
			if (overlapX <= 0f || overlapY <= 0f)
				return Vector2.zero;

			// 选择最小重叠方向进行排斥
			Vector2 force;
			if (overlapX < overlapY)
			{
				float sign = delta.x > 0f ? 1f : -1f;
				force = new Vector2(sign * overlapX * stiffness, 0f);
			}
			else if (overlapY < overlapX)
			{
				float sign = delta.y > 0f ? 1f : -1f;
				force = new Vector2(0f, sign * overlapY * stiffness);
			}
			else
			{
				// 重叠相等时双向排斥
				float signX = delta.x > 0f ? 1f : -1f;
				float signY = delta.y > 0f ? 1f : -1f;
				force = new Vector2(signX * overlapX * stiffness * 0.5f, signY * overlapY * stiffness * 0.5f);
			}

			return force;
		}
	}
}
