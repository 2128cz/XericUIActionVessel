using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 把材质渲染成图的执行者。<b>运行时与编辑器都能用</b>：全程不碰 UnityEditor，
	/// 不往场景里放相机或 GameObject——离屏渲染靠 CommandBuffer 自带视图/投影矩阵。
	///
	/// <para><b>用法</b>：<c>using (var r = new ShaderBakeRenderer(mat)) { var tex = r.RenderToTexture2D(); ... }</c>，
	/// 或者常驻一个实例反复改 <see cref="settings"/> 再 <see cref="Render"/>（预览就是这条路）。</para>
	///
	/// <para><b>已知的渲染环境限制</b>：没有真实相机，于是灯光、雾、环境光、阴影都不参与——
	/// Unlit 类 shader 效果一致，Surface/光照类 shader 会渲成"无灯"。要带光照请改用场景相机截帧，
	/// 那是另一条路，这里不假装支持。</para>
	/// </summary>
	public sealed class ShaderBakeRenderer : IDisposable
	{
		/// <summary> 怎么渲。外部可以直接改这个对象里的字段，改完调用 Render 就会跟着生效。 </summary>
		public ShaderBakeSettings settings = ShaderBakeSettings.CreateDefault();

		/// <summary> 渲什么。允许为 null（此时 Render 返回 null 并留一条 lastError）。 </summary>
		public Material material;

		/// <summary> 最近一次失败的原因；成功时清空。给界面状态栏用，不让用户去 Console 里找。 </summary>
		public string lastError = string.Empty;

		private Mesh m_Geometry;
		private string m_GeometrySignature = string.Empty;
		private RenderTexture m_OwnedTarget;
		private string m_TargetSignature = string.Empty;
		private RenderTexture m_MultiSampleSource;
		private CommandBuffer m_Buffer;
		private MaterialPropertyBlock m_Block;

		/// <summary> 当前这次烘焙用的网格（只读用途，外部别销毁） </summary>
		public Mesh geometry { get { return m_Geometry; } }

		/// <summary> 上一次渲染留下的目标（可能为 null）。预览直接拿它显示，省一次重建。 </summary>
		public RenderTexture target { get { return m_OwnedTarget; } }

		/// <summary> 面片在世界空间的包围盒（相机框位用） </summary>
		public Bounds geometryBounds
		{
			get { return ShaderBakeGeometryBuilder.GetWorldBounds(m_Geometry, settings.surface.localMatrix); }
		}

		/// <summary> 参数变了但还没重画：界面用它决定状态栏写"待刷新"还是"已渲染" </summary>
		public bool isDirty
		{
			get { return m_TargetSignature != settings.output.GetTargetSignature() || m_GeometrySignature != settings.surface.GetGeometrySignature(); }
		}

		/// <summary>
		/// 渲一次。<paramref name="target"/> 为空时用内部缓存的目标（尺寸变了自动重建），
		/// 非空时渲进调用方那张（要求宽高与 settings.output 一致，否则 Unity 只会画一部分）。
		/// 返回真正写入了结果的那张 RT，可能是传入的那张，也可能是内部那张。
		/// </summary>
		public RenderTexture Render(RenderTexture target = null)
		{
			lastError = string.Empty;

			if (material == null)
			{
				lastError = "没有材质：先给一个 Shader 或 Material";
				return null;
			}
			if (material.shader == null)
			{
				lastError = "材质上的 shader 丢了";
				return null;
			}

			settings.ClampAll();
			EnsureGeometry();
			if (m_Geometry == null)
			{
				lastError = "几何构建失败";
				return null;
			}

			RenderTexture dest = target ?? EnsureOwnedTarget();
			if (dest == null)
			{
				lastError = "目标纹理创建失败（尺寸或格式超出显卡限制）";
				return null;
			}

			// MSAA 目标不能直接被 ReadPixels 读到解析后的像素，所以先画进采样目标，再 Blit 回普通目标
			bool needResolve = settings.output.antiAliasing > 1 && target == null && m_MultiSampleSource != null;
			RenderTexture drawn = needResolve ? m_MultiSampleSource : dest;

			DrawInto(drawn);

			if (needResolve)
				Graphics.Blit(m_MultiSampleSource, dest);

			return dest;
		}

		/// <summary> 渲成可读 Texture2D（调用方负责销毁）。导出文件与存资产都从这里出发。 </summary>
		public Texture2D RenderToTexture2D(bool generateMips = false)
		{
			RenderTexture rt = Render();
			if (rt == null)
				return null;
			var texture = ShaderBakeReadback.ToTexture2D(rt, settings.output.textureFormat, settings.output.isLinearTexture, generateMips);
			texture.name = material != null ? material.name : "ShaderBakeResult";
			return texture;
		}

		/// <summary>
		/// 渲成 Sprite。返回的 Sprite 持有那张纹理，<b>别去销毁 sprite.texture</b>；
		/// 默认轴心在正中、100 像素=1 单位，和 Unity 新建 Sprite 的默认值一致。
		/// </summary>
		public Sprite RenderToSprite(float pixelsPerUnit = 100f, float pivotX = 0.5f, float pivotY = 0.5f)
		{
			Texture2D texture = RenderToTexture2D(false);
			if (texture == null)
				return null;
			return ShaderBakeReadback.ToSprite(texture, new Vector2(pivotX, pivotY), Mathf.Max(pixelsPerUnit, 0.01f), texture.name);
		}

		/// <summary> 渲成图片文件的字节。textureToKeep 非空时把中间纹理交出去由调用方销毁，否则就地销毁。 </summary>
		public byte[] RenderToBytes(ShaderBakeImageFormat format, out Texture2D textureToKeep)
		{
			textureToKeep = null;
			Texture2D texture = RenderToTexture2D(false);
			if (texture == null)
				return null;
			if (format.NeedsFloatSource() && !settings.output.hdr)
				Debug.LogWarning("[ShaderBake] EXR 建议配 HDR 输出，否则值域被截到 0~1");

			byte[] bytes = format.Encode(texture, settings.output.clearColor);
			textureToKeep = texture;
			return bytes;
		}

		/// <summary> 渲成图片文件的字节（中间纹理自动回收） </summary>
		public byte[] RenderToBytes(ShaderBakeImageFormat format, bool destroyTexture = true)
		{
			Texture2D texture;
			byte[] bytes = RenderToBytes(format, out texture);
			if (destroyTexture && texture != null)
				ReleaseTempObject(texture);
			return bytes;
		}

		/// <summary> 把相机挪到刚好框住面片。改了尺寸/位置之后按一下就回到"看得见"的状态。 </summary>
		public void FitCameraToGeometry()
		{
			EnsureGeometry();
			if (m_Geometry == null)
				return;
			settings.camera.FrameBounds(geometryBounds, settings.aspect);
		}

		/// <summary>
		/// 把相机挪到刚好框住<b>某一格</b>。
		///
		/// <para><b>为什么需要单独按格框</b>：<see cref="FitCameraToGeometry"/> 框的是整个网格，
		/// 多格时每格只占画面宽高的 1/列数、1/行数，还额外带 15% 余量。于是"场景里一张面片贴满物体"
		/// 的效果在这里变成画面正中的一小块，看上去像 shader 只画了九宫格的中间那格。
		/// 想要和场景一致的比例，就按这一格来框（余量取 1，不留边）。</para>
		/// </summary>
		public bool FitCameraOnFace(int faceIndex)
		{
			Bounds bounds;
			if (!TryGetFaceWorldBounds(faceIndex, out bounds))
				return false;
			// padding 给 1：这一格就是要铺满整张输出图，留边就等于把内容缩小
			settings.camera.FrameBounds(bounds, settings.aspect, 1f);
			return true;
		}

		/// <summary> 某一格在世界空间的包围盒（外部网格不参与，返回 false） </summary>
		public bool TryGetFaceWorldBounds(int faceIndex, out Bounds bounds)
		{
			bounds = default(Bounds);
			ShaderBakeSurfaceSettings surface = settings.surface;
			Vector2 rectMin, rectMax;
			if (!TryGetFaceLocalRect(surface, faceIndex, out rectMin, out rectMax))
				return false;

			Matrix4x4 local = surface.localMatrix;
			Vector3 min = new Vector3(rectMin.x, rectMin.y, 0f);
			Vector3 max = new Vector3(rectMax.x, rectMax.y, 0f);
			Vector3 worldMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 worldMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x,
				                             (i & 2) == 0 ? min.y : max.y,
				                             (i & 4) == 0 ? min.z : max.z);
				Vector3 world = local.MultiplyPoint3x4(corner);
				worldMin = Vector3.Min(worldMin, world);
				worldMax = Vector3.Max(worldMax, world);
			}
			bounds = new Bounds((worldMin + worldMax) * 0.5f, worldMax - worldMin);
			return true;
		}

		/// <summary> 某一格在面片局部空间里的矩形。网格整体以原点为中心，第 0 行在最下 </summary>
		private static bool TryGetFaceLocalRect(ShaderBakeSurfaceSettings surface, int faceIndex,
			out Vector2 rectMin, out Vector2 rectMax)
		{
			rectMin = Vector2.zero;
			rectMax = Vector2.zero;
			if (surface.kind == ShaderBakeSurfaceKind.Mesh)
				return false;

			int columns = Mathf.Max(surface.gridColumns, 1);
			int rows = Mathf.Max(surface.gridRows, 1);
			if (faceIndex < 0 || faceIndex >= columns * rows)
				return false;

			int column = faceIndex % columns;
			int row = faceIndex / columns;
			float x0 = -surface.size.x * columns * 0.5f + column * surface.size.x;
			float y0 = -surface.size.y * rows * 0.5f + row * surface.size.y;
			rectMin = new Vector2(x0, y0);
			rectMax = new Vector2(x0 + surface.size.x, y0 + surface.size.y);
			return true;
		}

		/// <summary>
		/// 把浏览图上的一个点（0~1，左上为原点）打到面片网络上，返回命中的格子序号。
		/// 用的是与烘焙完全相同的投影，所以"看到的哪一格"和"点到的哪一格"是同一套坐标。
		/// </summary>
		public bool TryPickFace(Vector2 viewport01, out int faceIndex)
		{
			faceIndex = -1;
			Matrix4x4 viewProjection;
			if (!TryGetViewProjection(out viewProjection))
				return false;

			ShaderBakeSurfaceSettings surface = settings.surface;
			float ndcX = viewport01.x * 2f - 1f;
			float ndcY = 1f - viewport01.y * 2f;
			Matrix4x4 inverse = viewProjection.inverse;
			Vector3 near = Unproject(inverse, new Vector3(ndcX, ndcY, -1f));
			Vector3 far = Unproject(inverse, new Vector3(ndcX, ndcY, 1f));
			Vector3 direction = far - near;

			Matrix4x4 local = surface.localMatrix;
			Vector3 planePoint = local.MultiplyPoint3x4(Vector3.zero);
			Vector3 planeNormal = local.MultiplyVector(Vector3.forward).normalized;
			float denominator = Vector3.Dot(direction, planeNormal);
			if (Mathf.Abs(denominator) < 0.000001f)
				return false;
			float distance = Vector3.Dot(planePoint - near, planeNormal) / denominator;
			if (distance < 0f)
				return false;

			int columns = Mathf.Max(surface.gridColumns, 1);
			int rows = Mathf.Max(surface.gridRows, 1);
			Vector3 localPoint = local.inverse.MultiplyPoint3x4(near + direction * distance);
			float totalWidth = surface.size.x * columns;
			float totalHeight = surface.size.y * rows;
			float u = (localPoint.x + totalWidth * 0.5f) / totalWidth;
			float v = (localPoint.y + totalHeight * 0.5f) / totalHeight;
			if (u < 0f || u > 1f || v < 0f || v > 1f)
				return false;
			int column = Mathf.Clamp(Mathf.FloorToInt(u * columns), 0, columns - 1);
			int row = Mathf.Clamp(Mathf.FloorToInt(v * rows), 0, rows - 1);
			faceIndex = row * columns + column;
			return true;
		}

		/// <summary>
		/// 某一格在浏览图上的矩形范围（0~1，左上为原点），用来把选中高亮贴在图上。
		/// 透视或旋转时这是一个外接框，不是精确的四边形轮廓。
		/// </summary>
		public bool TryGetFaceViewportBounds(int faceIndex, out Vector2 min, out Vector2 max)
		{
			min = Vector2.zero;
			max = Vector2.zero;
			Matrix4x4 viewProjection;
			if (!TryGetViewProjection(out viewProjection))
				return false;

			ShaderBakeSurfaceSettings surface = settings.surface;
			Vector2 rectMin, rectMax;
			if (!TryGetFaceLocalRect(surface, faceIndex, out rectMin, out rectMax))
				return false;

			float x0 = rectMin.x;
			float y0 = rectMin.y;
			float x1 = rectMax.x;
			float y1 = rectMax.y;

			Matrix4x4 local = surface.localMatrix;
			bool first = true;
			for (int i = 0; i < 4; i++)
			{
				Vector3 corner = new Vector3((i & 1) == 0 ? x0 : x1, (i & 2) == 0 ? y0 : y1, 0f);
				Vector4 clip = viewProjection * local.MultiplyPoint3x4(corner);
				if (clip.w <= 0.0001f)
					return false;
				float u = (clip.x / clip.w + 1f) * 0.5f;
				float v = 1f - (clip.y / clip.w + 1f) * 0.5f;
				if (float.IsNaN(u) || float.IsNaN(v))
					return false;
				var point = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
				if (first)
				{
					min = max = point;
					first = false;
				}
				else
				{
					min = Vector2.Min(min, point);
					max = Vector2.Max(max, point);
				}
			}
			return true;
		}

		private bool TryGetViewProjection(out Matrix4x4 viewProjection)
		{
			viewProjection = Matrix4x4.identity;
			if (settings == null || settings.surface == null)
				return false;
			// 外部网格不是平面，"点选格子"这件事对它没有意义
			if (settings.surface.kind == ShaderBakeSurfaceKind.Mesh && settings.surface.sourceMesh != null)
				return false;

			Matrix4x4 projection = settings.camera.GetProjectionMatrix(settings.aspect);
			if (settings.output.flipY)
				projection = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * projection;
			viewProjection = projection * settings.camera.viewMatrix;
			return true;
		}

		private static Vector3 Unproject(Matrix4x4 inverse, Vector3 clip)
		{
			Vector4 point = inverse * new Vector4(clip.x, clip.y, clip.z, 1f);
			float w = Mathf.Abs(point.w) < 0.0000001f ? 0.0000001f : point.w;
			return new Vector3(point.x / w, point.y / w, point.z / w);
		}

		/// <summary> 网格与相机指纹都记下来，界面据此判断要不要重建 </summary>
		public string stateSignature
		{
			get { return settings.output.GetTargetSignature() + "|" + settings.surface.GetGeometrySignature() + "|" + settings.camera.GetCameraSignature(); }
		}

		/// <summary>
		/// 只重建网格（面片参数变了）。渲染路径本来就会在签名不符时重建，这里给界面留个显式入口。
		/// </summary>
		public void InvalidateGeometry()
		{
			m_GeometrySignature = string.Empty;
		}

		private void EnsureGeometry()
		{
			string signature = settings.surface.GetGeometrySignature();
			if (m_Geometry != null && signature == m_GeometrySignature)
				return;

			ReleaseTempObject(m_Geometry);
			m_Geometry = ShaderBakeGeometryBuilder.Build(settings.surface);
			m_GeometrySignature = signature;
		}

		private RenderTexture EnsureOwnedTarget()
		{
			string signature = settings.output.GetTargetSignature();
			if (m_OwnedTarget != null && signature == m_TargetSignature)
				return m_OwnedTarget;

			ReleaseTempObject(m_OwnedTarget);
			ReleaseTempObject(m_MultiSampleSource);
			// 落地目标永远单采样：它要能被 ReadPixels 读、能被 Blit 写入、也直接拿去显示
			m_OwnedTarget = settings.output.CreateRenderTexture("ShaderBakeTarget", 1);
			m_TargetSignature = m_OwnedTarget != null ? signature : string.Empty;

			if (settings.output.antiAliasing > 1 && m_OwnedTarget != null)
				m_MultiSampleSource = settings.output.CreateRenderTexture("ShaderBakeMultiSample", settings.output.antiAliasing);

			return m_OwnedTarget;
		}

		private void DrawInto(RenderTexture rt)
		{
			if (m_Buffer == null)
			{
				m_Buffer = new CommandBuffer { name = "XericUI.ShaderBake" };
			}

			Matrix4x4 projection = settings.camera.GetProjectionMatrix(settings.aspect);
			if (settings.output.flipY)
				projection = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * projection;

			m_Buffer.Clear();
			m_Buffer.SetRenderTarget(rt, 0, CubemapFace.Unknown, 0);
			m_Buffer.ClearRenderTarget(true, true, settings.output.clearColor, 1f);
			m_Buffer.SetViewProjectionMatrices(settings.camera.viewMatrix, projection);
			// UV 已经烘进顶点了；只有用户显式要求时再补一份 _MainTex_ST 给依赖 ST 的 shader。
			// 走属性块而不是改材质，是为了不让这一笔写进材质副本、进而被"写回原材质"带走。
			MaterialPropertyBlock block = null;
			if (settings.surface.applyUvToMainTexST)
			{
				if (m_Block == null)
					m_Block = new MaterialPropertyBlock();
				m_Block.Clear();
				m_Block.SetVector("_MainTex_ST", new Vector4(
					settings.surface.uvTiling.x, settings.surface.uvTiling.y,
					settings.surface.uvOffset.x, settings.surface.uvOffset.y));
				block = m_Block;
			}

			m_Buffer.DrawMesh(m_Geometry, settings.surface.localMatrix, material,
				Mathf.Max(settings.surface.meshSubMesh, 0), settings.output.shaderPass, block);

			// 翻 Y 的投影是一次镜像，会把绕序反过来，背面剔除于是剔掉本该看见的正面。
			// 不动网格（外部模型的绕序不该被我们改），只在这一刻把剔除方向反过来。
			bool previousInvert = GL.invertCulling;
			GL.invertCulling = previousInvert != settings.output.flipY;
			List<Action> restoreGlobals = settings.globals != null ? settings.globals.Apply() : null;
			try
			{
				Graphics.ExecuteCommandBuffer(m_Buffer);
			}
			finally
			{
				GL.invertCulling = previousInvert;
				// 画完立刻还原：这些是编辑器全局状态，留一次烘焙的值下来会污染别处的渲染
				if (restoreGlobals != null)
				{
					for (int i = restoreGlobals.Count - 1; i >= 0; i--)
						restoreGlobals[i]();
				}
			}
			m_Buffer.Clear();
		}

		/// <summary>
		/// 回收一次性对象。运行时要走 <c>Destroy</c>（当帧不能立即销毁），编辑器里请传
		/// <paramref name="immediate"/> 让 <c>DestroyImmediate</c> 立刻放掉显存——导一批图时差别很大。
		/// </summary>
		public static void ReleaseTempObject(UnityEngine.Object obj)
		{
			ReleaseTempObject(obj, false);
		}

		public static void ReleaseTempObject(UnityEngine.Object obj, bool immediate)
		{
			if (obj == null)
				return;
#if UNITY_EDITOR
			if (immediate || !Application.isPlaying)
				UnityEngine.Object.DestroyImmediate(obj);
			else
				UnityEngine.Object.Destroy(obj);
#else
			UnityEngine.Object.Destroy(obj);
#endif
		}

		public void Dispose()
		{
			Dispose(false);
		}

		/// <summary> 编辑器里关窗口时传 immediate，显存和网格内存当场还回去 </summary>
		public void Dispose(bool immediate)
		{
			if (m_Buffer != null)
			{
				// 只 Dispose：Dispose 内部已经 Release，再单独 Release 一次会二次释放，
				// 2021.3 上表现为关闭窗口时抛 _unity_self 为空的 ArgumentNullException
				try
				{
					m_Buffer.Dispose();
				}
				catch (Exception)
				{
					// 退出编辑器/域重载期间原生对象可能已被回收，这时再报错没有意义
				}
				m_Buffer = null;
			}
			ReleaseTempObject(m_MultiSampleSource, immediate);
			ReleaseTempObject(m_OwnedTarget, immediate);
			ReleaseTempObject(m_Geometry, immediate);
			m_MultiSampleSource = null;
			m_OwnedTarget = null;
			m_Geometry = null;
			m_TargetSignature = string.Empty;
			m_GeometrySignature = string.Empty;
		}
	}
}
