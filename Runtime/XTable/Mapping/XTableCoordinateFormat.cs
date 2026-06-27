using System.Text;

namespace XericUI.XTable.Mapping
{
	/// <summary>
	/// 表格坐标文本格式化工具——
	/// 列索引 ↔ 字母（A..Z, AA..ZZ）、单元格文本 ↔ (row,col)、范围文本 ↔ 矩形选区。
	/// </summary>
	public static class XTableCoordinateFormat
	{
		private const string ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		#region 列字母转换

		/// <summary>列索引转 Excel 风格字母（0→A, 25→Z, 26→AA）</summary>
		public static string ColumnIndexToLetter(int col)
		{
			if (col < 0) return string.Empty;

			var sb = new StringBuilder(4);
			do
			{
				sb.Insert(0, ALPHABET[col % 26]);
				col = col / 26 - 1;
			} while (col >= 0);
			return sb.ToString();
		}

		/// <summary>Excel 字母转列索引（A→0, Z→25, AA→26）</summary>
		public static int LetterToColumnIndex(string letter)
		{
			if (string.IsNullOrEmpty(letter)) return -1;

			int result = 0;
			foreach (char c in letter.ToUpperInvariant())
			{
				if (c < 'A' || c > 'Z') return -1;
				result = result * 26 + (c - 'A' + 1);
			}
			return result - 1;
		}

		#endregion

		#region 单元格文本 ↔ 索引

		/// <summary>单元格索引 → "A0" 格式文本</summary>
		public static string CellToText(int row, int col)
		{
			return $"{ColumnIndexToLetter(col)}{row}";
		}

		/// <summary>"A0" 格式文本 → (row, col)，解析失败返回 false</summary>
		public static bool TryParseCell(string text, out int row, out int col)
		{
			row = -1;
			col = -1;
			if (string.IsNullOrEmpty(text)) return false;

			// 分离字母前缀和数字后缀
			int i = 0;
			while (i < text.Length && char.IsLetter(text[i])) i++;
			if (i == 0 || i == text.Length) return false;

			string colPart = text.Substring(0, i);
			string rowPart = text.Substring(i);

			col = LetterToColumnIndex(colPart);
			if (col < 0) return false;
			if (!int.TryParse(rowPart, out row) || row < 0) return false;
			return true;
		}

		#endregion

		#region 范围文本 ↔ 矩形选区

		/// <summary>矩形选区 → "A0:B1" 格式（统一使用左上:右下）</summary>
		public static string RangeToText(int startRow, int startCol, int endRow, int endCol)
		{
			NormalizeRange(ref startRow, ref startCol, ref endRow, ref endCol);
			return $"{ColumnIndexToLetter(startCol)}{startRow}:{ColumnIndexToLetter(endCol)}{endRow}";
		}

		/// <summary>"A0:B1" 格式文本 → 矩形选区参数，解析失败返回 false</summary>
		public static bool TryParseRange(string text,
			out int startRow, out int startCol,
			out int endRow, out int endCol)
		{
			startRow = -1; startCol = -1; endRow = -1; endCol = -1;
			if (string.IsNullOrEmpty(text)) return false;

			int colon = text.IndexOf(':');
			if (colon < 0)
			{
				// 无冒号，尝试作为单个单元格解析
				if (TryParseCell(text, out startRow, out startCol))
				{
					endRow = startRow;
					endCol = startCol;
					return true;
				}
				return false;
			}

			string left = text.Substring(0, colon);
			string right = text.Substring(colon + 1);

			if (!TryParseCell(left, out startRow, out startCol)) return false;
			if (!TryParseCell(right, out endRow, out endCol)) return false;

			return true;
		}

		/// <summary>将命名范围标准化（确保 start ≤ end），返回 (minR, minC, maxR, maxC)</summary>
		public static void NormalizeRange(ref int startRow, ref int startCol,
			ref int endRow, ref int endCol)
		{
			if (startRow > endRow) (startRow, endRow) = (endRow, startRow);
			if (startCol > endCol) (startCol, endCol) = (endCol, startCol);
		}

		#endregion
	}
}
