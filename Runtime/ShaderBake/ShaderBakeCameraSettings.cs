using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 离屏"摄像机"参数。这里没有真的 Camera——投影与视口矩阵是自己算出来塞进 CommandBuffer 的，
	/// 好处是不往场景里塞任何临时物体，坏处是 API 相关的 Y 翻转要自己管（见 output.flipY）。
	/// </summary>
	[Serializable]
	public class ShaderBakeCameraSettings
	{
		[Tooltip("正交（默认）：面片不会因为透视而变形，适合贴图类 shader")]
		public bool orthographic = true;

		[Tooltip("正交半高（世界单位）：实际能看到的竖向范围是它的两倍")]
		public float orthographicSize = 0.6f;

		[Tooltip("透视相机的竖向视野角（度），正交时忽略")]
		public float fieldOfView = 60f;

		[Tooltip("近裁剪面")]
		public float nearClipPlane = 0.01f;

		[Tooltip("远裁剪面")]
		public float farClipPlane = 100f;

		[Tooltip("相机位置：正交时只有朝向影响画面，位置决定远近")]
		public Vector3 position = new Vector3(0f, 0f, -10f);

		[Tooltip("相机旋转（欧拉角，度）：默认朝 +Z 看，即看着放在原点、面朝 -Z 的面片")]
		public Vector3 eulerAngles;

		/// <summary>
		/// 视口（世界 → 相机）矩阵。
		///
		/// <para><b>为什么要镜像 Z</b>：<see cref="Matrix4x4.Perspective"/> 与 <see cref="Matrix4x4.Ortho"/>
		/// 沿用 OpenGL 约定——相机朝 <b>-Z</b> 看；而 <c>TRS(...).inverse</c> 给出的是 Unity 自己的
		/// "朝 +Z 看"。两者直接拼起来，正前方的物体会得到负的裁剪 w（透视）或落在近远裁剪面之外（正交），
		/// 整块几何被裁光，表现就是"渲染成功但画面全透明"。</para>
		/// </summary>
		public Matrix4x4 viewMatrix
		{
			get
			{
				Matrix4x4 worldToLocal = Matrix4x4.TRS(position, Quaternion.Euler(eulerAngles), Vector3.one).inverse;
				return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * worldToLocal;
			}
		}

		/// <summary>
		/// 投影矩阵。正交时宽高按 aspect 分配（半宽 = 半高 × aspect），与 Unity 相机一致。
		/// </summary>
		public Matrix4x4 GetProjectionMatrix(float aspect)
		{
			if (aspect <= 0f) aspect = 1f;
			if (orthographic)
			{
				float halfHeight = Mathf.Max(orthographicSize, 0.0001f);
				float halfWidth = halfHeight * aspect;
				return Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight,
					Mathf.Max(nearClipPlane, 0.0001f), Mathf.Max(farClipPlane, nearClipPlane + 0.0001f));
			}
			return Matrix4x4.Perspective(Mathf.Clamp(fieldOfView, 1f, 179f), aspect,
				Mathf.Max(nearClipPlane, 0.0001f), Mathf.Max(farClipPlane, nearClipPlane + 0.0001f));
		}

		/// <summary>
		/// 把包围盒框进画面：正交改半高，透视改距离；同时把远近裁剪面推到包围盒外侧，
		/// 免得贴脸的大面片被近裁剪面削掉一块。
		/// </summary>
		public void FrameBounds(Bounds bounds, float aspect, float padding = 1.15f)
		{
			Vector3 extents = bounds.extents;
			float radius = extents.magnitude;
			if (radius < 0.0001f) radius = 0.0001f;
			if (aspect <= 0f) aspect = 1f;

			if (orthographic)
			{
				// 横向不够宽时以横向为准：半高 = 半宽 / aspect
				float halfFromHeight = extents.y;
				float halfFromWidth = extents.x / aspect;
				orthographicSize = Mathf.Max(halfFromHeight, halfFromWidth) * Mathf.Max(padding, 1f);
			}
			else
			{
				float halfFov = Mathf.Clamp(fieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad;
				float verticalFit = extents.y;
				float horizontalFit = extents.x / aspect;
				float neededHalfHeight = Mathf.Max(verticalFit, horizontalFit) * Mathf.Max(padding, 1f);
				float distance = radius + neededHalfHeight / Mathf.Max(Mathf.Tan(halfFov), 0.0001f);

				// 沿当前朝向平移，不改朝向——转着找角度是用户的事，这里只负责"离远一点"
				Vector3 dir = Quaternion.Euler(eulerAngles) * Vector3.forward;
				position = bounds.center - dir * Mathf.Max(distance, radius + 0.0001f);
			}

			float distanceFromCamera = Vector3.Distance(position, bounds.center);
			nearClipPlane = Mathf.Max(0.0001f, distanceFromCamera - radius * 3f);
			farClipPlane = distanceFromCamera + radius * 3f;
		}

		/// <summary>
		/// 收拢数值。负数在这组参数里没有任何含义，但数字框拦不住一个负号，而它造成的后果是
		/// "渲染成功、画面全空"：半高为负时投影矩阵被钳成一条线，什么都框不住，还不报任何错。
		/// 取绝对值而不是就地归零，是因为负号多半是手滑，量级本身是用户想要的。
		/// </summary>
		public void Clamp()
		{
			orthographicSize = Mathf.Max(Mathf.Abs(orthographicSize), 0.0001f);
			fieldOfView = Mathf.Clamp(Mathf.Abs(fieldOfView), 1f, 179f);
			nearClipPlane = Mathf.Max(Mathf.Abs(nearClipPlane), 0.0001f);
			farClipPlane = Mathf.Max(Mathf.Abs(farClipPlane), nearClipPlane + 0.0001f);
		}

		/// <summary> 摄像机指纹：变了不需要重建网格，只需要重画 </summary>
		public string GetCameraSignature()
		{
			return orthographic + "_" + orthographicSize + "_" + fieldOfView + "_" +
			       nearClipPlane + "_" + farClipPlane + "_" + position + "_" + eulerAngles;
		}
	}
}
