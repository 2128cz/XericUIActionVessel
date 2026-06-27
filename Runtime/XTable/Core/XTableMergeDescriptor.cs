using System;
using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 合并单元格描述——所有被合并单元格的 id 指向合并源（左上角单元格）。
	/// 同一类型支持本地块内引用和跨块引用，通过 <see cref="IsCrossBlock"/> 区分。
	/// </summary>
	[Serializable]
	public class XTableMergeDescriptor : ISerializationCallbackReceiver
	{
		/// <summary>合并区域在本地块内的起始行</summary>
		public int LocalStartRow;

		/// <summary>合并区域在本地块内的起始列</summary>
		public int LocalStartCol;

		/// <summary>合并跨越的行数</summary>
		public int MergeRowSpan;

		/// <summary>合并跨越的列数</summary>
		public int MergeColSpan;

		/// <summary>是否为跨块引用（true=合并源在另一个块上）</summary>
		public bool IsCrossBlock;

		/// <summary>
		/// 跨块引用时：源块在字典中的 key（Z 曲线索引）
		/// 本地引用时：忽略此字段
		/// </summary>
		public ulong SourceBlockIndex;

		/// <summary>源单元格在块内的行号（跨块时=源块的行、本地时=本块的行）</summary>
		public int SourceLocalRow;

		/// <summary>源单元格在块内的列号（跨块时=源块的列、本地时=本块的列）</summary>
		public int SourceLocalCol;

		/// <summary>本块内被合并的 (row, col) 集合——不参与序列化，通过代理列表同步</summary>
		[NonSerialized] public HashSet<(int row, int col)> MergedCellSet;

		// ── HashSet 序列化代理 ──
		[SerializeField] private List<int> m_MergedRowList = new List<int>();
		[SerializeField] private List<int> m_MergedColList = new List<int>();

		public XTableMergeDescriptor()
		{
			MergedCellSet = new HashSet<(int, int)>();
		}

		/// <summary>
		/// 判断指定本地坐标是否在此合并区域内
		/// </summary>
		public bool Contains(int localRow, int localCol)
		{
			return MergedCellSet.Contains((localRow, localCol));
		}

		/// <summary>
		/// 获取重定向目标——返回 (目标块Z索引, 目标块内行, 目标块内列)
		/// 本地引用时目标块索引为 0（由调用方忽略）
		/// </summary>
		public void GetRedirectTarget(out ulong targetBlockIndex, out int targetRow, out int targetCol)
		{
			targetBlockIndex = IsCrossBlock ? SourceBlockIndex : 0;
			targetRow = SourceLocalRow;
			targetCol = SourceLocalCol;
		}

		#region 序列化回调

		public void OnBeforeSerialize()
		{
			// HashSet<(int,int)> → 两个平行列表
			m_MergedRowList.Clear();
			m_MergedColList.Clear();
			if (MergedCellSet != null)
			{
				foreach (var (row, col) in MergedCellSet)
				{
					m_MergedRowList.Add(row);
					m_MergedColList.Add(col);
				}
			}
		}

		public void OnAfterDeserialize()
		{
			// 两个平行列表 → HashSet<(int,int)>
			MergedCellSet = new HashSet<(int, int)>();
			int count = System.Math.Min(
				m_MergedRowList?.Count ?? 0,
				m_MergedColList?.Count ?? 0);
			for (int i = 0; i < count; i++)
			{
				MergedCellSet.Add((m_MergedRowList[i], m_MergedColList[i]));
			}
		}

		#endregion
	}
}
