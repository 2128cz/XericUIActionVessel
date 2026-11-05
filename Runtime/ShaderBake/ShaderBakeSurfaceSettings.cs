using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 一个面片的 UV 区域编号：面片 (0,0) 用 UV [0,1]，左边一格是 [-1,0]，右边一格是 [1,2]，以此类推。
	/// 网格里按行优先存放（第 0 行是 y 最小的一行）。
	/// </summary>
	[Serializable]
	public class ShaderBakeFaceCell
	{
		public int uvX;
		public int uvY;

		public ShaderBakeFaceCell() { }

		public ShaderBakeFaceCell(int x, int y)
		{
			uvX = x;
			uvY = y;
		}
	}

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

		[Tooltip("单张面片的尺寸（世界单位）。网格整体是它的 列数×行数 倍；外部网格只在 normalizeSize 打开时用它缩放")]
		public Vector2 size = Vector2.one;

		[Tooltip("细分平面的行列段数。只有需要顶点插值的 shader 才要调大，四边形恒为 1x1")]
		public Vector2Int segments = Vector2Int.one;

		[Tooltip("面片网格列数。每多一列就多一张 UV 区域，整体尺寸不变")]
		public int gridColumns = 1;

		[Tooltip("面片网格行数")]
		public int gridRows = 1;

		[Tooltip("每个面片对应的 UV 区域编号（行优先，第 0 行在下）。默认只有一格 (0,0)")]
		public List<ShaderBakeFaceCell> faces = new List<ShaderBakeFaceCell> { new ShaderBakeFaceCell(0, 0) };

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

		/// <summary> 面片网格单边上限：再多单张图已经容纳不下可辨认的细节了 </summary>
		public const int kMaxGrid = 16;

		public int FaceCount { get { return gridColumns * gridRows; } }

		public ShaderBakeFaceCell GetFace(int index)
		{
			EnsureFaces();
			return faces[Mathf.Clamp(index, 0, faces.Count - 1)];
		}

		/// <summary>
		/// 让 faces 的数量与 gridColumns × gridRows 对齐。补出来的格子默认 (0,0)，
		/// 真正的 UV 编号由 <see cref="AddColumn"/> / <see cref="AddRow"/> 按邻格推算。
		/// </summary>
		public void EnsureFaces()
		{
			gridColumns = Mathf.Clamp(gridColumns, 1, kMaxGrid);
			gridRows = Mathf.Clamp(gridRows, 1, kMaxGrid);
			if (faces == null)
				faces = new List<ShaderBakeFaceCell>();

			int wanted = FaceCount;
			while (faces.Count < wanted)
				faces.Add(new ShaderBakeFaceCell(0, 0));
			if (faces.Count > wanted)
				faces.RemoveRange(wanted, faces.Count - wanted);
			for (int i = 0; i < faces.Count; i++)
				if (faces[i] == null)
					faces[i] = new ShaderBakeFaceCell(0, 0);
		}

		/// <summary>
		/// 在左边/右边加一列。新列的 UV 编号取贴边那一列的编号往外挪一格——
		/// 这正是"左边加的面是 [-1,0]"这条规则的落点。
		/// </summary>
		public void AddColumn(bool right)
		{
			EnsureFaces();
			if (gridColumns >= kMaxGrid)
				return;

			for (int row = gridRows - 1; row >= 0; row--)
			{
				int rowStart = row * gridColumns;
				int anchor = right ? rowStart + gridColumns - 1 : rowStart;
				ShaderBakeFaceCell source = faces[anchor];
				var cell = new ShaderBakeFaceCell(source.uvX + (right ? 1 : -1), source.uvY);
				faces.Insert(right ? anchor + 1 : rowStart, cell);
			}
			gridColumns++;
		}

		/// <summary> 在上边/下边加一行，UV 编号同样从贴着的那一行往外推 </summary>
		public void AddRow(bool top)
		{
			EnsureFaces();
			if (gridRows >= kMaxGrid)
				return;

			int offset = top ? faces.Count : 0;
			int anchorRow = top ? gridRows - 1 : 0;
			var added = new ShaderBakeFaceCell[gridColumns];
			for (int col = 0; col < gridColumns; col++)
			{
				ShaderBakeFaceCell source = faces[anchorRow * gridColumns + col];
				added[col] = new ShaderBakeFaceCell(source.uvX, source.uvY + (top ? 1 : -1));
			}
			faces.InsertRange(offset, added);
			gridRows++;
		}

		/// <summary> 回到单片 (0,0) —— 网格加乱了时的退路 </summary>
		public void ResetGrid()
		{
			gridColumns = 1;
			gridRows = 1;
			faces = new List<ShaderBakeFaceCell> { new ShaderBakeFaceCell(0, 0) };
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
			EnsureFaces();
		}

		private static float FixZero(float v)
		{
			return Mathf.Abs(v) < 0.0001f ? 1f : v;
		}

		/// <summary> 几何指纹：变了就要重建网格 </summary>
		public string GetGeometrySignature()
		{
			var meshId = sourceMesh != null ? sourceMesh.GetInstanceID().ToString() : "none";
			var builder = new System.Text.StringBuilder(64);
			for (int i = 0; i < faces.Count; i++)
				builder.Append(faces[i].uvX).Append(',').Append(faces[i].uvY).Append(';');
			return kind + "_" + size + "_" + segments + "_" + uvOffset + "_" + uvTiling +
			       "_" + position + "_" + eulerAngles + "_" + scale + "_" + meshId + "_" + meshSubMesh +
			       "_" + normalizeMeshSize + "_" + gridColumns + "x" + gridRows + "_" + builder;
		}
	}
}
