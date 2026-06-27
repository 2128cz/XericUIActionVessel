using UnityEditor;

using UnityEngine;

using XericUI.XTable.Mapping;
using XericUI.XTable.Rendering.Component;

namespace XericUIEditor.XTable
{
	/// <summary>
	/// Scene View 表格编辑覆盖层——
	/// 左侧行索引、顶部列索引（A..Z）、底部输入框。
	/// 点击行列交叉区域选中单元格，底部输入框编辑文本，方向键导航，Shift+方向键多选。
	/// 不创建任何场景对象，通过 Handles.BeginGUI/EndGUI 绘制。
	/// </summary>
	[InitializeOnLoad]
	public static class XericUIActionTableSceneEditor
	{
		private static bool s_Enabled;
		private const string ENABLED_KEY = "XericUI_TableSceneEditor_Enabled";

		private static string s_EditText = "";

		// 拖拽框选状态
		private static bool s_IsDragging;
		private static bool s_DragFromLabel;    // 拖拽是否从行/列标签发起
		private static int s_DragStartRow = -1;
		private static int s_DragStartCol = -1;
		private static Rect s_TableRect;      // 当前表格区域（用于拖拽期间鼠标检测）
		private static float s_XScale, s_YScale;
		private static float[] s_RowHeights, s_ColWidths;
		private static XericUIActionTable s_ActiveTable;

		private static readonly Color s_LabelBg = new Color(0.12f, 0.12f, 0.18f, 0.9f);
		private static readonly Color s_SelectedLabel = new Color(0.2f, 0.5f, 0.9f, 0.6f);
		private static readonly Color s_MultiSelectLabel = new Color(0.2f, 0.5f, 0.9f, 0.35f);
		private static readonly Color s_LabelText = new Color(0.75f, 0.75f, 0.8f);
		private static readonly Color s_InputBg = new Color(0.08f, 0.08f, 0.14f, 0.92f);
		private static readonly Color s_SelectionColor = new Color(0.18f, 0.45f, 0.85f, 0.25f);
		private static readonly Color s_MultiSelectRangeColor = new Color(0.18f, 0.45f, 0.85f, 0.12f);

		private const float ROW_H = 22f;
		private const float COL_W = 36f;
		private const float MARGIN = 3f;

		static XericUIActionTableSceneEditor()
		{
			s_Enabled = EditorPrefs.GetBool(ENABLED_KEY, true);
			SceneView.duringSceneGui += OnSceneGUI;
		}

		[MenuItem("Xeric UI/表格编辑器覆盖层", false, 200)]
		private static void ToggleEnabled()
		{
			s_Enabled = !s_Enabled;
			EditorPrefs.SetBool(ENABLED_KEY, s_Enabled);
			Menu.SetChecked("Xeric UI/表格编辑器覆盖层", s_Enabled);
			SceneView.RepaintAll();
		}

		[MenuItem("Xeric UI/表格编辑器覆盖层", true)]
		private static bool ToggleEnabledValidate()
		{
			Menu.SetChecked("Xeric UI/表格编辑器覆盖层", s_Enabled);
			return true;
		}

		private static void OnSceneGUI(SceneView sceneView)
		{
			if (!s_Enabled) return;

			GameObject sel = Selection.activeGameObject;
			if (sel == null) return;

			XericUIActionTable table = sel.GetComponentInChildren<XericUIActionTable>();
			if (table == null)
				table = sel.GetComponentInParent<XericUIActionTable>();
			if (table == null || !table.isActiveAndEnabled) return;

#if UNITY_EDITOR
			Canvas.ForceUpdateCanvases();
#endif

			RectTransform tableRt = table.GetComponent<RectTransform>();
			float[] rh = table.RowHeights;
			float[] cw = table.ColWidths;
			if (rh == null || cw == null || rh.Length == 0 || cw.Length == 0) return;

			Vector3[] corners = new Vector3[4];
			tableRt.GetWorldCorners(corners);

			Vector2 guiTL = HandleUtility.WorldToGUIPoint(corners[1]);
			Vector2 guiBR = HandleUtility.WorldToGUIPoint(corners[3]);

			float tableW = guiBR.x - guiTL.x;
			float tableH = guiBR.y - guiTL.y;
			if (tableW <= 0 || tableH <= 0) return;

			float totalW = 0, totalH = 0;
			foreach (float w in cw) totalW += w;
			foreach (float h in rh) totalH += h;
			if (totalW <= 0 || totalH <= 0) return;

			float xScale = tableW / totalW;
			float yScale = tableH / totalH;

			Rect tableRect = new Rect(guiTL.x, guiTL.y, tableW, tableH);

			Handles.BeginGUI();

			DrawRowLabels(rh, tableRect, xScale, yScale, table);
			DrawColumnLabels(cw, tableRect, xScale, yScale, table);
			DrawGridLines(rh, cw, tableRect, xScale, yScale);
			DrawSelectionHighlight(rh, cw, tableRect, xScale, yScale, table);
			DrawInputBar(table, tableRect.x, tableRect.y + tableRect.height + MARGIN);
			HandleInput(table, rh, cw, tableRect, xScale, yScale);
			HandleKeyboard(table, rh, cw);

			Handles.EndGUI();
		}

		#region 辅助

		private static int IndexFromCoord(float coord, float[] sizes)
		{
			float acc = 0;
			for (int i = 0; i < sizes.Length; i++)
			{
				if (coord >= acc && coord < acc + sizes[i]) return i;
				acc += sizes[i];
			}
			return -1;
		}

		/// <summary>判断某行是否在多选范围内</summary>
		private static bool IsRowInMultiSelect(XericUIActionTable table, int row)
		{
			if (!table.HasMultiSelection) return false;
			int minR = Mathf.Min(table.SelectStartRow, table.SelectEndRow);
			int maxR = Mathf.Max(table.SelectStartRow, table.SelectEndRow);
			return row >= minR && row <= maxR;
		}

		/// <summary>判断某列是否在多选范围内</summary>
		private static bool IsColInMultiSelect(XericUIActionTable table, int col)
		{
			if (!table.HasMultiSelection) return false;
			int minC = Mathf.Min(table.SelectStartCol, table.SelectEndCol);
			int maxC = Mathf.Max(table.SelectStartCol, table.SelectEndCol);
			return col >= minC && col <= maxC;
		}

		/// <summary>更新 s_EditText 为当前焦点格的单元格内容</summary>
		private static void UpdateEditText(XericUIActionTable table, int row, int col)
		{
			s_EditText = table.GetCellData(row, col).Text ?? "";
		}

		#endregion

		#region 行标签

		private static void DrawRowLabels(float[] rh, Rect tableRect,
			float xScale, float yScale, XericUIActionTable table)
		{
			var style = new GUIStyle(GUI.skin.label)
			{
				fontSize = 10,
				normal = { textColor = s_LabelText },
				alignment = TextAnchor.MiddleCenter
			};

			float y = tableRect.y;
			for (int r = 0; r < rh.Length; r++)
			{
				float cellH = rh[r] * yScale;
				if (cellH < 2) { y += cellH; continue; }

				Rect labelRect = new Rect(
					tableRect.x - COL_W - MARGIN,
					y,
					COL_W,
					cellH);

				Color bgColor;
				if (table.SelectedRow == r)
					bgColor = s_SelectedLabel;
				else if (IsRowInMultiSelect(table, r))
					bgColor = s_MultiSelectLabel;
				else
					bgColor = s_LabelBg;

				EditorGUI.DrawRect(labelRect, bgColor);
				GUI.Label(labelRect, $"{r}", style);

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int col = table.SelectedCol;
					if (col < 0) col = 0;

					s_IsDragging = true;
					s_DragFromLabel = true;
					s_DragStartRow = r;
					s_DragStartCol = col;

					if (Event.current.shift && table.SelectedRow >= 0)
					{
						if (!table.HasMultiSelection)
							table.StartMultiSelect(table.SelectedRow, table.SelectedCol);
						table.ExtendMultiSelect(r, col);
					}
					else
					{
						table.StartMultiSelect(r, col);
					}

					table.RefreshView();
					UpdateEditText(table, r, col);
					Event.current.Use();
					SceneView.RepaintAll();
				}

				y += cellH;
			}
		}

		#endregion

		#region 列标签

		private static void DrawColumnLabels(float[] cw, Rect tableRect,
			float xScale, float yScale, XericUIActionTable table)
		{
			var style = new GUIStyle(GUI.skin.label)
			{
				fontSize = 10,
				normal = { textColor = s_LabelText },
				alignment = TextAnchor.MiddleCenter
			};

			float x = tableRect.x;
			for (int c = 0; c < cw.Length; c++)
			{
				float cellW = cw[c] * xScale;
				if (cellW < 2) { x += cellW; continue; }

				Rect labelRect = new Rect(
					x,
					tableRect.y - ROW_H - MARGIN,
					cellW,
					ROW_H);

				Color bgColor;
				if (table.SelectedCol == c)
					bgColor = s_SelectedLabel;
				else if (IsColInMultiSelect(table, c))
					bgColor = s_MultiSelectLabel;
				else
					bgColor = s_LabelBg;

				EditorGUI.DrawRect(labelRect, bgColor);

				string colLetter = XTableCoordinateFormat.ColumnIndexToLetter(c);
				string text = cellW > 40 ? colLetter : (cellW > 18 ? colLetter : "");
				GUI.Label(labelRect, text, style);

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int row = table.SelectedRow;
					if (row < 0) row = 0;

					s_IsDragging = true;
					s_DragFromLabel = true;
					s_DragStartRow = row;
					s_DragStartCol = c;

					if (Event.current.shift && table.SelectedCol >= 0)
					{
						if (!table.HasMultiSelection)
							table.StartMultiSelect(table.SelectedRow, table.SelectedCol);
						table.ExtendMultiSelect(row, c);
					}
					else
					{
						table.StartMultiSelect(row, c);
					}

					table.RefreshView();
					UpdateEditText(table, row, c);
					Event.current.Use();
					SceneView.RepaintAll();
				}

				x += cellW;
			}
		}

		#endregion

		#region 网格线

		private static void DrawGridLines(float[] rh, float[] cw, Rect tr,
			float xScale, float yScale)
		{
			Color lineColor = new Color(0.5f, 0.5f, 0.55f, 0.18f);

			float y = tr.y;
			for (int r = 0; r < rh.Length; r++)
			{
				y += rh[r] * yScale;
				EditorGUI.DrawRect(new Rect(tr.x, y, tr.width, 1), lineColor);
			}

			float x = tr.x;
			for (int c = 0; c < cw.Length; c++)
			{
				x += cw[c] * xScale;
				EditorGUI.DrawRect(new Rect(x, tr.y, 1, tr.height), lineColor);
			}
		}

		#endregion

		#region 选中高亮

		private static void DrawSelectionHighlight(float[] rh, float[] cw, Rect tr,
			float xScale, float yScale, XericUIActionTable table)
		{
			int sr = table.SelectedRow;
			int sc = table.SelectedCol;
			if (sr < 0 || sc < 0 || sr >= rh.Length || sc >= cw.Length) return;

			// 多选范围高亮
			if (table.HasMultiSelection)
			{
				int minR = Mathf.Min(table.SelectStartRow, table.SelectEndRow);
				int maxR = Mathf.Max(table.SelectStartRow, table.SelectEndRow);
				int minC = Mathf.Min(table.SelectStartCol, table.SelectEndCol);
				int maxC = Mathf.Max(table.SelectStartCol, table.SelectEndCol);

				minR = Mathf.Clamp(minR, 0, rh.Length - 1);
				maxR = Mathf.Clamp(maxR, 0, rh.Length - 1);
				minC = Mathf.Clamp(minC, 0, cw.Length - 1);
				maxC = Mathf.Clamp(maxC, 0, cw.Length - 1);

				float msx = tr.x + SumBefore(cw, minC) * xScale;
				float msy = tr.y + SumBefore(rh, minR) * yScale;
				float msw = SumRange(cw, minC, maxC + 1) * xScale;
				float msh = SumRange(rh, minR, maxR + 1) * yScale;

				EditorGUI.DrawRect(new Rect(msx + 1, msy + 1, msw - 2, msh - 2),
					s_MultiSelectRangeColor);
			}

			// 焦点格高亮（始终绘制）
			float sx = tr.x + SumBefore(cw, sc) * xScale;
			float sy = tr.y + SumBefore(rh, sr) * yScale;
			float sw = cw[sc] * xScale;
			float sh = rh[sr] * yScale;

			EditorGUI.DrawRect(new Rect(sx + 1, sy + 1, sw - 2, sh - 2),
				s_SelectionColor);
		}

		private static float SumBefore(float[] arr, int count)
		{
			float sum = 0;
			for (int i = 0; i < count && i < arr.Length; i++) sum += arr[i];
			return sum;
		}

		private static float SumRange(float[] arr, int start, int end)
		{
			float sum = 0;
			for (int i = start; i < end && i < arr.Length; i++) sum += arr[i];
			return sum;
		}

		#endregion

		#region 底部输入框

		private static void DrawInputBar(XericUIActionTable table, float x, float y)
		{
			Rect barRect = new Rect(x, y, 500, 24);
			EditorGUI.DrawRect(barRect, s_InputBg);

			if (table.SelectedRow >= 0 && table.SelectedCol >= 0)
			{
				var labelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					normal = { textColor = s_LabelText },
					alignment = TextAnchor.MiddleCenter
				};

				// 单元格坐标（单选 "A0"，多选 "A0:B1"）
				string cellLabel = table.GetSelectionText();
				GUI.Label(new Rect(barRect.x + 4, barRect.y, 75, barRect.height),
					cellLabel, labelStyle);

				var inputStyle = new GUIStyle(GUI.skin.textField)
				{
					fontSize = 12,
					margin = new RectOffset(2, 2, 2, 2)
				};
				Rect inputRect = new Rect(barRect.x + 85, barRect.y + 2, 200, barRect.height - 4);

				// 回车键提交
				if (Event.current.isKey && Event.current.keyCode == KeyCode.Return
					&& GUI.GetNameOfFocusedControl() == "XTableSceneEditInput")
				{
					ApplyEdit(table);
					Event.current.Use();
				}

				GUI.SetNextControlName("XTableSceneEditInput");
				s_EditText = GUI.TextField(inputRect, s_EditText, inputStyle);

				var btnStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 11,
					margin = new RectOffset(2, 2, 2, 2),
					padding = new RectOffset(4, 4, 2, 2)
				};
				if (GUI.Button(new Rect(barRect.x + 370, barRect.y + 2, 50, barRect.height - 4),
					"应用", btnStyle))
				{
					GUI.FocusControl(null);
					ApplyEdit(table);
				}
			}
			else
			{
				var hintStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
					alignment = TextAnchor.MiddleCenter
				};
				GUI.Label(barRect, "点击左侧行号或上方列名选中单元格 | Shift+方向键多选", hintStyle);
			}
		}

		private static void ApplyEdit(XericUIActionTable table)
		{
			if (table.SelectedRow < 0 || table.SelectedCol < 0) return;
			table.SetCellText(table.SelectedRow, table.SelectedCol, s_EditText);
			table.RefreshView();
			SceneView.RepaintAll();
		}

		#endregion

		#region 表格区域点击与拖拽

		private static void HandleInput(XericUIActionTable table,
			float[] rh, float[] cw, Rect tr,
			float xScale, float yScale)
		{
			Event e = Event.current;

			// 更新当前帧的表格参数缓存，供拖拽期间使用
			s_TableRect = tr;
			s_XScale = xScale;
			s_YScale = yScale;
			s_RowHeights = rh;
			s_ColWidths = cw;
			s_ActiveTable = table;

			switch (e.type)
			{
				case EventType.MouseDown:
					if (e.button != 0) break;
					if (!tr.Contains(e.mousePosition)) break;

					CellFromMouse(tr, xScale, yScale, out int mdRow, out int mdCol);
					if (mdRow < 0 || mdCol < 0) break;

					s_IsDragging = true;
					s_DragFromLabel = false;
					s_DragStartRow = mdRow;
					s_DragStartCol = mdCol;

					if (e.shift && table.SelectedRow >= 0 && table.SelectedCol >= 0)
					{
						if (!table.HasMultiSelection)
							table.StartMultiSelect(table.SelectedRow, table.SelectedCol);
						table.ExtendMultiSelect(mdRow, mdCol);
					}
					else
					{
						table.StartMultiSelect(mdRow, mdCol);
					}

					table.RefreshView();
					UpdateEditText(table, mdRow, mdCol);
					e.Use();
					SceneView.RepaintAll();
					break;

				case EventType.MouseDrag:
					if (!s_IsDragging || e.button != 0) break;

					CellFromMouse(tr, xScale, yScale, out int dragRow, out int dragCol);
					if (dragRow < 0 || dragCol < 0) break;

					table.ExtendMultiSelect(dragRow, dragCol);
					table.RefreshView();
					UpdateEditText(table, dragRow, dragCol);
					e.Use();
					SceneView.RepaintAll();
					break;

				case EventType.MouseUp:
					if (e.button != 0) break;
					s_IsDragging = false;
					break;
			}
		}

		private static void CellFromMouse(Rect tr, float xScale, float yScale,
			out int row, out int col)
		{
			float rx = (Event.current.mousePosition.x - tr.x) / xScale;
			float ry = (Event.current.mousePosition.y - tr.y) / yScale;
			col = IndexFromCoord(rx, s_ColWidths);
			row = IndexFromCoord(ry, s_RowHeights);
		}

		/// <summary>被行标签/列标签拖拽时调用，将拖拽扩展到整行/整列方向</summary>
		private static void StartDragFromLabel(int row, int col, XericUIActionTable table)
		{
			s_IsDragging = true;
			s_DragStartRow = row;
			s_DragStartCol = col;
			table.StartMultiSelect(row, col);
		}

		#endregion

		#region 键盘快捷键

		private static void HandleKeyboard(XericUIActionTable table, float[] rh, float[] cw)
		{
			Event e = Event.current;
			if (e.type != EventType.KeyDown) return;

			// 输入框获得焦点时不拦截方向键（用于文本光标移动）
			if (GUI.GetNameOfFocusedControl() == "XTableSceneEditInput")
				return;

			// Escape 清除选区
			if (e.keyCode == KeyCode.Escape)
			{
				table.ClearSelection();
				table.RefreshView();
				s_EditText = "";
				e.Use();
				SceneView.RepaintAll();
				return;
			}

			int row = table.SelectedRow;
			int col = table.SelectedCol;
			bool shift = e.shift;

			// 无选中时，方向键自动选中第一个单元格
			if (row < 0 || col < 0)
			{
				if (rh.Length == 0 || cw.Length == 0) return;
				row = 0;
				col = 0;

				switch (e.keyCode)
				{
					case KeyCode.UpArrow:
					case KeyCode.DownArrow:
					case KeyCode.LeftArrow:
					case KeyCode.RightArrow:
						break;
					default:
						return;
				}

				table.SelectCell(row, col);
				table.RefreshView();
				UpdateEditText(table, row, col);
				e.Use();
				SceneView.RepaintAll();
				return;
			}

			switch (e.keyCode)
			{
				case KeyCode.UpArrow:
					if (!TryClampToValid(row - 1, col, rh, cw, out row, out col)) return;
					break;
				case KeyCode.DownArrow:
					if (!TryClampToValid(row + 1, col, rh, cw, out row, out col)) return;
					break;
				case KeyCode.LeftArrow:
					if (!TryClampToValid(row, col - 1, rh, cw, out row, out col)) return;
					break;
				case KeyCode.RightArrow:
					if (!TryClampToValid(row, col + 1, rh, cw, out row, out col)) return;
					break;
				default:
					return;
			}

			if (shift)
			{
				if (!table.HasMultiSelection && table.SelectedRow >= 0 && table.SelectedCol >= 0)
					table.StartMultiSelect(table.SelectedRow, table.SelectedCol);
				table.ExtendMultiSelect(row, col);
			}
			else
			{
				table.SelectCell(row, col);
			}

			UpdateEditText(table, row, col);
			table.RefreshView();
			e.Use();
			SceneView.RepaintAll();
		}

		private static bool TryClampToValid(int row, int col, float[] rh, float[] cw,
			out int outRow, out int outCol)
		{
			if (row < 0 || row >= rh.Length || col < 0 || col >= cw.Length)
			{
				outRow = row;
				outCol = col;
				return false;
			}
			outRow = row;
			outCol = col;
			return true;
		}

		#endregion
	}
}
