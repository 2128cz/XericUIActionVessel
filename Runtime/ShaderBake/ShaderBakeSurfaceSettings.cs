using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary> 被烘焙的几何面片是哪一种 </summary>
	public enum ShaderBakeSurfaceKind
	{
		/// <summary>单片四边形：两个三角面，UV 从 uvOffset 起、按 uvTiling 铺 </summary>
		Quad,

		/// <summary>细分平面：顶点数量由 segments 决定，给"在顶点着色器里挪顶点"的 shader 留出梯度 </summary>
		Plane,

		/// <summary>外部网格：模型（导入的或场景里挑的），UV 沿用网格自己的第 0 套再乘偏移缩放 </summary>
		Mesh
	}

	/// <summary>
	/// 面片设置：形状、UV 变换与面片自身的变换（"面片编辑"就是这一组数值）。
	///
	/// <para><b>UV 变换落在网格上而不是材质上</b>：这样 shader 里怎么写 <c>uv</c> 就怎么收到结果，
	/// 不会被 <c>_MainTex_ST</c> 之类的第二层缩放重复一遍。需要走 ST 的那类 shader 用
	/// <see cref="applyUvToMainTexST"/> 单独开。</para>
	/// </summary>
	[Serializable]
	public class ShaderBakeSurfaceSettings
	{
		/// <summary> 细分上限：单边 512 段＝26 万顶点，再多就是把手抖拖进卡顿 </summary>
		public const int kMaxSegments = 512;

		[Tooltip("面片种类：单片四边形 / 细分平面 / 外部网格")]
		public ShaderBakeSurfaceKind kind = ShaderBakeSurfaceKind.Quad;

		[Tooltip("面片尺寸（世界单位）。四边形与细分平面用它的 x/y；外部网格只在 normalizeSize 打开时用它缩放")]
		public Vector2 size = Vector2.one;

		[Tooltip("细分平面的行列段数。只有需要顶点插值的 shader 才要调大，四边形恒为 1x1")]
		public Vector2Int segments = Vector2Int.one;

		[Tooltip("UV 起点偏移：把贴图的哪一块挪到面片左下角")]
		public Vector2 uvOffset;

		[Tooltip("UV 重复次数（缩放）：x/y 各铺几遍")]
		public Vector2 uvTiling = Vector2.one;

		[Tooltip("外部网格（kind = 外部网格 时生效）")]
		public Mesh sourceMesh;

		[Tooltip("外部网格用第几个 subMesh")]
		public int meshSubMesh;

		[Tooltip("把外部网格归一化到 size 指定的尺寸（关掉就按模型原始米数渲，可能只占画面一角）")]
		public bool normalizeMeshSize = true;

		[Tooltip("面片位置偏移")]
		public Vector3 position;

		[Tooltip("面片旋转（欧拉角，度）")]
		public Vector3 eulerAngles;

		[Tooltip("面片额外缩放（与 size 相乘，外部网格用它做整体缩放）")]
		public Vector3 scale = Vector3.one;

		[Tooltip("把 UV 偏移/重复同时写进 _MainTex_ST：给 Standard / Unlit 那类靠 ST 做缩放的 shader 用")]
		public bool applyUvToMainTexST;

		/// <summary> 面片自己的局部变换矩阵（DrawMesh 用的那个 model 矩阵） </summary>
		public Matrix4x4 localMatrix
		{
			get { return Matrix4x4.TRS(position, Quaternion.Euler(eulerAngles), scale); }
		}

		/// <summary>
		/// 收拢非法数值。段数与 subMesh 都可能被敲成负数或超出网格实际层数，这里一次归位。
		/// </summary>
		public void Clamp()
		{
			size = new Vector2(
				Mathf.Max(size.x, 0.0001f),
				Mathf.Max(size.y, 0.0001f));
			segments = new Vector2Int(
				Mathf.Clamp(segments.x, 1, kMaxSegments),
				Mathf.Clamp(segments.y, 1, kMaxSegments));
			scale = new Vector3(
				FixZero(scale.x),
				FixZero(scale.y),
				FixZero(scale.z));
			if (uvTiling.x == 0f) uvTiling.x = 1f;
			if (uvTiling.y == 0f) uvTiling.y = 1f;
			meshSubMesh = Mathf.Max(meshSubMesh, 0);
		}

		private static float FixZero(float v)
		{
			return Mathf.Abs(v) < 0.0001f ? 1f : v;
		}

		/// <summary> 几何指纹：变了就要重建网格 </summary>
		public string GetGeometrySignature()
		{
			var meshId = sourceMesh != null ? sourceMesh.GetInstanceID().ToString() : "none";
			return kind + "_" + size + "_" + segments + "_" + uvOffset + "_" + uvTiling +
			       "_" + position + "_" + eulerAngles + "_" + scale + "_" + meshId + "_" + meshSubMesh +
			       "_" + normalizeMeshSize;
		}
	}
}
