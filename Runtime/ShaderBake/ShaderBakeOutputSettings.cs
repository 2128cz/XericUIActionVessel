using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 输出目标设置：决定"烘出来那张 RenderTexture 长什么样"。
	///
	/// <para><b>为什么 flipY 要摊开给用户</b>：这里走的是 CommandBuffer + 自建投影矩阵的离屏渲染，
	/// 没有相机帮你做 API 相关的 Y 翻转，于是同一段代码在 D3D 与 OpenGL/Metal 上读回来的像素是上下相反的。
	/// 与其替用户猜一个方向，不如把开关留在界面上（规范：一次调用不出结果时先怀疑方向，再怀疑内容）。</para>
	/// </summary>
	[Serializable]
	public class ShaderBakeOutputSettings
	{
		/// <summary> 边长下限 </summary>
		public const int kMinDimension = 1;

		/// <summary>
		/// 边长上限：4096² 的 RGBA32 读回是 64MB 内存，ARGBFloat 是 256MB。
		/// 再往上就不是"烘焙一张图"而是"烘一张图集"了，交给调用方自己决定要不要冒险。
		/// </summary>
		public const int kMaxDimension = 4096;

		[Tooltip("导出图片的宽（像素）")]
		public int width = 512;

		[Tooltip("导出图片的高（像素）")]
		public int height = 512;

		[Tooltip("HDR：目标改用 ARGBFloat。开这项时请配 EXR 导出，PNG/JPG 存不下超过 1 的分量")]
		public bool hdr;

		[Tooltip("多重采样，只能是 1 / 2 / 4 / 8；渲染完会自动解析回普通纹理再读回")]
		public int antiAliasing = 1;

		[Tooltip("深度位数。只渲一张面片填 0 就够，渲模型（有多层遮挡）要留 24")]
		public int depthBuffer = 24;

		[Tooltip("目标按 sRGB 存储。关掉＝线性，通常配 HDR + EXR 自己走色彩管理")]
		public bool sRGB = true;

		[Tooltip("背景色。alpha 留 0 才能导出带透明通道的图；导 JPG 时这个颜色会被压进图里")]
		public Color clearColor = new Color(0f, 0f, 0f, 0f);

		[Tooltip("翻转投影 Y：画面上下颠倒时打开（不同图形 API 的读回方向不一样）")]
		public bool flipY;

		[Tooltip("subShader 的 pass 下标，-1＝所有 pass 叠加渲染")]
		public int shaderPass = -1;

		[Tooltip("目标纹理的过滤方式")]
		public FilterMode filterMode = FilterMode.Bilinear;

		[Tooltip("目标纹理的寻址方式")]
		public TextureWrapMode wrapMode = TextureWrapMode.Clamp;

		/// <summary> 目标的 RenderTexture 格式 </summary>
		public RenderTextureFormat format
		{
			get { return hdr ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32; }
		}

		/// <summary> 目标的色彩存储方式（HDR 只能线性） </summary>
		public RenderTextureReadWrite renderTextureColorSpace
		{
			get { return hdr || !sRGB ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB; }
		}

		/// <summary> 读回成 Texture2D 时用的像素格式 </summary>
		public TextureFormat textureFormat
		{
			get { return hdr ? TextureFormat.RGBAFloat : TextureFormat.RGBA32; }
		}

		/// <summary> 读回来的 Texture2D 是否按线性解释 </summary>
		public bool isLinearTexture
		{
			get { return hdr || !sRGB; }
		}

		/// <summary> 目标纹理的有效宽高（读回、贴图、算尺寸都走这个，别各自再算一遍） </summary>
		public Vector2Int size
		{
			get { return new Vector2Int(width, height); }
		}

		/// <summary>
		/// 把用户敲进来的数值收进合法区间。非法值不报错、只归位——界面上每改一次都走这里，
		/// 报错会把输入框变成打字障碍。
		/// </summary>
		public void Clamp()
		{
			width = Mathf.Clamp(width, kMinDimension, kMaxDimension);
			height = Mathf.Clamp(height, kMinDimension, kMaxDimension);
			depthBuffer = Mathf.Clamp(depthBuffer, 0, 32);
			// HDR 浮点目标基本没有平台支持 MSAA，硬开着只会得到一张黑图
			if (hdr)
				antiAliasing = 1;
			else
				antiAliasing = SnapAntiAliasing(antiAliasing);
		}

		/// <summary> 距离最近的一个合法采样倍数（1/2/4/8） </summary>
		public static int SnapAntiAliasing(int requested)
		{
			if (requested <= 1) return 1;
			if (requested <= 2) return 2;
			if (requested <= 4) return 4;
			return 8;
		}

		/// <summary>
		/// 目标设置指纹：变了就要重建 RenderTexture。尺寸、格式、采样、深度都会换掉显存布局。
		/// </summary>
		public string GetTargetSignature()
		{
			return width + "x" + height + "_" + (hdr ? "f" : "l") + "_" + antiAliasing + "_" + depthBuffer + "_" + (sRGB ? "s" : "l");
		}

		/// <summary>
		/// 按当前设置造一张未被使用过的 RenderTexture（调用方负责销毁）。
		/// <paramref name="samples"/> 传负数表示按设置走；解析目标必须显式传 1——
		/// 往多采样目标上 Blit 是非法的，MSAA 那条路要靠"先画采样目标、再落地到单采样目标"。
		/// </summary>
		public RenderTexture CreateRenderTexture(string name = "ShaderBakeTarget", int samples = -1)
		{
			Clamp();
			int actualSamples = samples < 0 ? antiAliasing : SnapAntiAliasing(samples);
			if (hdr && actualSamples > 1)
				actualSamples = 1;
			var rt = new RenderTexture(width, height, depthBuffer, format, renderTextureColorSpace)
			{
				name = name,
				filterMode = filterMode,
				wrapMode = wrapMode,
				antiAliasing = actualSamples,
				hideFlags = HideFlags.DontSave
			};
			// 显式 Create：antiAliasing 只有在初始化之前赋值才生效，靠第一次使用时自动建就丢了
			rt.Create();
			return rt;
		}
	}
}
