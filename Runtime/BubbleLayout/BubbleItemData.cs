using System;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 气泡布局中每个子UI元素的物理状态数据
	/// </summary>
	[Serializable]
	public class BubbleItemData
	{
		/// <summary>
		/// 关联的 RectTransform
		/// </summary>
		[NonSerialized]
		public RectTransform rectTransform;

		/// <summary>
		/// 唯一标识键（名称+实例ID格式）
		/// </summary>
		public string Key;

		/// <summary>
		/// 当前动画位置（世界/局部坐标，取决于布局设置）
		/// </summary>
		public Vector2 position;

		/// <summary>
		/// UI元素尺寸
		/// </summary>
		public Vector2 size;

		/// <summary>
		/// 旋转角度（度）
		/// </summary>
		public float rotation;

		// --- 物理状态 ---

		/// <summary>当前速度</summary>
		public Vector2 velocity;

		/// <summary>上一帧速度</summary>
		public Vector2 prevVelocity;

		/// <summary>当前受到的力</summary>
		public Vector2 force;

		/// <summary>上一帧受到的力</summary>
		public Vector2 prevForce;

		/// <summary>动量</summary>
		public Vector2 momentum;

		/// <summary>角动量</summary>
		public float angularMomentum;

		/// <summary>质量（默认=1）</summary>
		public float mass = 1f;

		/// <summary>
		/// 获取以position为中心的Rect
		/// </summary>
		public Rect GetRect()
		{
			return new Rect(position.x - size.x * 0.5f, position.y - size.y * 0.5f, size.x, size.y);
		}

		/// <summary>
		/// 从RectTransform构建数据
		/// </summary>
		public static BubbleItemData FromRectTransform(RectTransform rt)
		{
			var data = new BubbleItemData
			{
				rectTransform = rt,
				Key = $"{rt.name}_{rt.GetInstanceID()}",
				position = rt.localPosition,
				size = rt.rect.size,
				rotation = rt.localEulerAngles.z,
				velocity = Vector2.zero,
				prevVelocity = Vector2.zero,
				force = Vector2.zero,
				prevForce = Vector2.zero,
				momentum = Vector2.zero,
				angularMomentum = 0f,
				mass = 1f,
			};
			return data;
		}

		/// <summary>
		/// 将当前position应用回RectTransform
		/// </summary>
		public void ApplyToTransform()
		{
			if (rectTransform != null)
			{
				rectTransform.localPosition = new Vector3(position.x, position.y, rectTransform.localPosition.z);
			}
		}
	}
}
