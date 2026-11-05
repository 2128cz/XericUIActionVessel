using System;
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
			m_OwnedTarget = settings.output.CreateRenderTexture("ShaderBakeTarget");
			m_TargetSignature = m_OwnedTarget != null ? signature : string.Empty;

			if (settings.output.antiAliasing > 1 && m_OwnedTarget != null)
				m_MultiSampleSource = settings.output.CreateRenderTexture("ShaderBakeMultiSample");

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
			m_Buffer.DrawMesh(m_Geometry, settings.surface.localMatrix, material,
				Mathf.Max(settings.surface.meshSubMesh, 0), settings.output.shaderPass);
			Graphics.ExecuteCommandBuffer(m_Buffer);
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

		internal static void ReleaseTempObject(UnityEngine.Object obj, bool immediate)
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
				m_Buffer.Release();
				m_Buffer.Dispose();
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
