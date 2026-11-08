using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 按 <see cref="ShaderBakeSurfaceSettings"/> 造出这次烘焙要画的那张网格。
	///
	/// <para><b>外部网格一定要复制</b>：UV 偏移/重复是写进顶点数据的，直接改
	/// <c>MeshFilter.sharedMesh</c> 等于改了项目里的资产。这里把顶点搬到一张新网格上，
	/// 蒙皮信息不带过来（烘焙用不到，带过来反而要把骨骼矩阵一起搬）。</para>
	/// </summary>
	public static class ShaderBakeGeometryBuilder
	{
		/// <summary> 造一张新网格（调用方负责销毁）。kind 为外部网格但没有指定网格时退回单片四边形。 </summary>
		public static Mesh Build(ShaderBakeSurfaceSettings surface)
		{
			if (surface == null)
				return null;
			surface.Clamp();

			switch (surface.kind)
			{
				case ShaderBakeSurfaceKind.Plane:
					return BuildFaces(surface, Mathf.Max(surface.segments.x, 1), Mathf.Max(surface.segments.y, 1));
				case ShaderBakeSurfaceKind.Mesh when surface.sourceMesh != null:
					return BuildFromMesh(surface);
				default:
					return BuildFaces(surface, 1, 1);
			}
		}

		/// <summary>
		/// 世界空间的包围盒：网格局部包围盒乘面片变换。给"框住面片"用，所以要在外面算，
		/// 免得网格和变换的耦合漏进几何构建里。
		/// </summary>
		public static Bounds GetWorldBounds(Mesh mesh, Matrix4x4 matrix)
		{
			if (mesh == null)
				return new Bounds(Vector3.zero, Vector3.one);
			if (matrix.isIdentity)
				return mesh.bounds;

			Bounds b = mesh.bounds;
			Vector3 min = b.min, max = b.max;
			Vector3 worldMin = matrix.MultiplyPoint(min);
			Vector3 worldMax = worldMin;

			// 旋转过之后轴对齐盒子的八个角才是新盒子的候选极值，只转两个角会得到一个歪掉的框
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = matrix.MultiplyPoint(new Vector3(
					(i & 1) == 0 ? min.x : max.x,
					(i & 2) == 0 ? min.y : max.y,
					(i & 4) == 0 ? min.z : max.z));
				worldMin = Vector3.Min(worldMin, corner);
				worldMax = Vector3.Max(worldMax, corner);
			}

			var bounds = new Bounds();
			bounds.SetMinMax(worldMin, worldMax);
			return bounds;
		}

		/// <summary>
		/// 按面片网格造一张平面：每个格子是一张完整的面片，UV 从自己格子的编号起算。
		/// <paramref name="segmentsX"/>/<paramref name="segmentsY"/> 是<b>单张面片内部</b>的细分，
		/// 给顶点着色器留梯度用；格子本身恒为一张。
		/// </summary>
		private static Mesh BuildFaces(ShaderBakeSurfaceSettings surface, int segmentsX, int segmentsY)
		{
			int columns = Mathf.Max(surface.gridColumns, 1);
			int rows = Mathf.Max(surface.gridRows, 1);
			segmentsX = Mathf.Max(segmentsX, 1);
			segmentsY = Mathf.Max(segmentsY, 1);

			// size 是"每一格"的尺寸：加一格，整个平面就往那一侧长一格出去
			float faceWidth = surface.size.x;
			float faceHeight = surface.size.y;
			float halfWidth = faceWidth * columns * 0.5f;
			float halfHeight = faceHeight * rows * 0.5f;

			int verticesPerFace = (segmentsX + 1) * (segmentsY + 1);
			int vertexCount = columns * rows * verticesPerFace;
			var vertices = new List<Vector3>(vertexCount);
			var normals = new List<Vector3>(vertexCount);
			var uvs = new List<Vector2>(vertexCount);
			var triangles = new List<int>(columns * rows * segmentsX * segmentsY * (surface.doubleSided ? 12 : 6));

			int stride = segmentsX + 1;
			for (int row = 0; row < rows; row++)
			for (int column = 0; column < columns; column++)
			{
				ShaderBakeFaceCell face = surface.GetFace(row * columns + column);
				float originX = -halfWidth + column * faceWidth;
				float originY = -halfHeight + row * faceHeight;
				int baseIndex = vertices.Count;

				for (int y = 0; y <= segmentsY; y++)
				{
					float ty = (float)y / segmentsY;
					for (int x = 0; x <= segmentsX; x++)
					{
						float tx = (float)x / segmentsX;
						vertices.Add(new Vector3(originX + tx * faceWidth, originY + ty * faceHeight, 0f));
						normals.Add(Vector3.back);   // 面朝 -Z，即默认相机（朝 +Z 看）看到的正面
						// 格子编号 + 面内 0~1，再统一套上全局的偏移/重复
						uvs.Add(RemapUv(new Vector2(face.uvX + tx, face.uvY + ty), surface));
					}
				}

				for (int y = 0; y < segmentsY; y++)
				for (int x = 0; x < segmentsX; x++)
				{
					int bottomLeft = baseIndex + y * stride + x;
					int bottomRight = bottomLeft + 1;
					int topLeft = bottomLeft + stride;
					int topRight = topLeft + 1;
					// 与 Unity 内置 Quad 同向：从法线指的那一侧看是 xy 平面的逆时针。
					// 究竟哪一侧算正面由图形 API 决定，而这条链路的投影矩阵是自己拼的、没有相机帮忙统一，
					// 所以单靠一个固定绕序永远会在某个 API 上被 Cull Back 剔光——见 doubleSided。
					AddQuad(triangles, bottomLeft, bottomRight, topRight, topLeft, false);
					if (surface.doubleSided)
						AddQuad(triangles, bottomLeft, bottomRight, topRight, topLeft, true);
				}
			}

			var mesh = NewMesh(vertexCount > 65000 ? "ShaderBakeFaces" : "ShaderBakeQuad");
			if (vertexCount > 65000)
				mesh.indexFormat = IndexFormat.UInt32;
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			Upload(mesh);
			return mesh;
		}

		/// <summary>
		/// 一个格子的两个三角面。<paramref name="reverse"/> 为真时把绕序整个反过来：同一片几何
		/// 在"顺时针算正面"与"逆时针算正面"两种约定下各有一份是可见的，而另一份仍会被剔除，
		/// 所以不会画两遍、透明混合也不会被叠加两次。
		/// </summary>
		private static void AddQuad(List<int> triangles, int bottomLeft, int bottomRight, int topRight, int topLeft, bool reverse)
		{
			if (reverse)
			{
				triangles.Add(bottomLeft); triangles.Add(bottomRight); triangles.Add(topLeft);
				triangles.Add(bottomRight); triangles.Add(topRight); triangles.Add(topLeft);
				return;
			}
			triangles.Add(bottomLeft); triangles.Add(bottomRight); triangles.Add(topRight);
			triangles.Add(bottomLeft); triangles.Add(topRight); triangles.Add(topLeft);
		}

		/// <summary>
		/// 复制外部网格的第 0 套 UV（没有就按位置做平面投影），再套上偏移/重复。
		/// </summary>
		private static Mesh BuildFromMesh(ShaderBakeSurfaceSettings surface)
		{
			Mesh source = surface.sourceMesh;
			int subMesh = Mathf.Min(surface.meshSubMesh, Mathf.Max(source.subMeshCount - 1, 0));

			var vertices = new List<Vector3>();
			source.GetVertices(vertices);
			if (vertices.Count == 0)
			{
				Debug.LogWarning("[ShaderBake] 外部网格没有顶点数据（可能是只读网格），退回单片四边形");
				return BuildFaces(surface, 1, 1);
			}

			var normals = new List<Vector3>();
			if (source.HasVertexAttribute(VertexAttribute.Normal))
				source.GetNormals(normals);

			var tangents = new List<Vector4>();
			if (source.HasVertexAttribute(VertexAttribute.Tangent))
				source.GetTangents(tangents);

			var uvs = new List<Vector2>();
			if (source.HasVertexAttribute(VertexAttribute.TexCoord0))
				source.GetUVs(0, uvs);
			if (uvs.Count != vertices.Count)
				uvs = BuildPlanarUv(vertices);

			// UV 是"面片的贴图坐标"，不该被模型的尺寸归一化影响，所以先 remap 再缩放顶点
			for (int i = 0; i < uvs.Count; i++)
				uvs[i] = RemapUv(uvs[i], surface);

			var mesh = NewMesh("ShaderBakeMesh");
			if (vertices.Count > 65000)
				mesh.indexFormat = IndexFormat.UInt32;

			Bounds srcBounds = source.bounds;
			Vector3 normalizeScale = surface.normalizeMeshSize ? FitScale(srcBounds.size, surface.size) : Vector3.one;

			for (int i = 0; i < vertices.Count; i++)
				vertices[i] = Vector3.Scale(vertices[i] - srcBounds.center, normalizeScale);

			mesh.SetVertices(vertices);
			if (normals.Count == vertices.Count) mesh.SetNormals(normals); else mesh.RecalculateNormals();
			if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
			mesh.SetUVs(0, uvs);

			// 只搬到选中的那一层：后面的层不该跟着画出来，但 subMesh 之前的层要一起画（多层模型常有共用材质球的分层）
			int layerCount = subMesh + 1;
			mesh.subMeshCount = layerCount;
			for (int i = 0; i < layerCount; i++)
			{
				int[] tris = source.GetTriangles(i);
				if (surface.doubleSided)
				{
					// 反向补一份，理由同 AddQuad：正面对侧的约定由 API 决定，别让烘焙结果取决于显卡
					int complete = tris.Length / 3 * 3;
					var both = new int[tris.Length + complete];
					tris.CopyTo(both, 0);
					for (int t = 0; t < complete; t += 3)
					{
						both[tris.Length + t] = tris[t];
						both[tris.Length + t + 1] = tris[t + 2];
						both[tris.Length + t + 2] = tris[t + 1];
					}
					tris = both;
				}
				mesh.SetTriangles(tris, i);
			}

			Upload(mesh);
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary> 把源尺寸装进目标尺寸：x/y 各自贴合，z 跟 x/y 的平均倍率走以免模型被压扁成纸片 </summary>
		private static Vector3 FitScale(Vector3 sourceSize, Vector2 targetSize)
		{
			float sx = sourceSize.x > 0.0001f ? targetSize.x / sourceSize.x : 1f;
			float sy = sourceSize.y > 0.0001f ? targetSize.y / sourceSize.y : 1f;
			float sz = sourceSize.z > 0.0001f ? (sx + sy) * 0.5f : 1f;
			return new Vector3(sx, sy, sz);
		}

		/// <summary> 网格没带 UV 时的兜底：拿 x/y 位置做一次平面投影，至少贴图有坐标可用 </summary>
		private static List<Vector2> BuildPlanarUv(List<Vector3> vertices)
		{
			var min = new Vector3(float.MaxValue, float.MaxValue, 0f);
			var max = new Vector3(float.MinValue, float.MinValue, 0f);
			foreach (Vector3 v in vertices)
			{
				min = Vector3.Min(min, v);
				max = Vector3.Max(max, v);
			}

			float rangeX = Mathf.Max(max.x - min.x, 0.0001f);
			float rangeY = Mathf.Max(max.y - min.y, 0.0001f);

			var uvs = new List<Vector2>(vertices.Count);
			foreach (Vector3 v in vertices)
				uvs.Add(new Vector2((v.x - min.x) / rangeX, (v.y - min.y) / rangeY));
			return uvs;
		}

		private static Vector2 RemapUv(Vector2 uv, ShaderBakeSurfaceSettings surface)
		{
			return Vector2.Scale(uv, surface.uvTiling) + surface.uvOffset;
		}

		private static Mesh NewMesh(string name)
		{
			return new Mesh
			{
				name = name,
				hideFlags = HideFlags.DontSave
			};
		}

		/// <summary> 上传为只读以外的可变网格（false）：重建时还要读回顶点，别让 Unity 把它锁掉 </summary>
		private static void Upload(Mesh mesh)
		{
			mesh.UploadMeshData(false);
		}
	}
}
