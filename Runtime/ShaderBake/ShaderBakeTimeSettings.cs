using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// 时间轴：单张渲染注入的 <c>_Time</c>，以及序列导出（视频 / 宫格图 / Texture3D）的帧数与步长。
	///
	/// <para><b>为什么要把时间做成参数</b>：离屏渲染没有"当前帧"这回事，
	/// 同一套参数连渲两次必须得到同一张图，否则导出的序列是花的、复现问题也没法对齐。
	/// 所以时间由这里显式给定，渲染时注入、渲染完还原，不跟着编辑器的帧走。</para>
	/// </summary>
	[Serializable]
	public class ShaderBakeTimeSettings
	{
		[Tooltip("注入的 _Time（秒）。单张导出与序列的第一帧都用它")]
		public float seconds;

		[Tooltip("序列每帧前进多少秒：它同时就是时间倍率，把动画放慢/加快都靠它")]
		public float frameStep = 1f / 30f;

		[Tooltip("序列帧数（视频帧数 / 宫格图格数 / 3D 纹理深度）")]
		public int frameCount = 64;

		[Tooltip("宫格图每行多少列，0＝按帧数自动取近似正方形")]
		public int sheetColumns;

		public const int kMaxFrames = 4096;

		public void Clamp()
		{
			frameCount = Mathf.Clamp(frameCount, 1, kMaxFrames);
			sheetColumns = Mathf.Clamp(sheetColumns, 0, 256);
		}

		/// <summary> 第 <paramref name="frame"/> 帧渲染用的时间 </summary>
		public float TimeOf(int frame)
		{
			return seconds + frameStep * frame;
		}

		/// <summary> 宫格图的列数：填了就用填的，0 时取能排成正接近正方形的列数 </summary>
		public int ColumnsFor(int frames)
		{
			if (sheetColumns > 0)
				return Mathf.Min(sheetColumns, frames);
			return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(frames)));
		}
	}
}
