using UnityEditor;

using UnityEngine;
using XericLibrary.Runtime.MacroLibrary;
using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.XTable.Core;
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
		// ─────────────────────────── 交互状态机 ───────────────────────────
		private enum EditorState
		{
			TableNavigation,   // 默认：键盘/鼠标在表格上导航与选择
			SizeEditing,       // 气泡输入框中编辑行高/列宽
			CellEditing        // 底部工具栏中编辑单元格文本内容
		}

		private static EditorState s_State;
		private static string s_CurrentControlName;  // 当前聚焦的控件名（每帧由焦点决定）

		private static bool s_Enabled;
		private const string ENABLED_KEY = "XericUI_TableSceneEditor_Enabled";

		private static string s_EditText = "";
		private static string s_SizeEditText = "";    // 气泡输入框当前编辑文本
		private static int s_SizeEditTargetIndex = -1;
		private static bool s_SizeEditIsColumn;
		private static string s_LastFocusedControl = "";

		// 拖拽框选状态
		private static bool s_IsDragging;
		private static int s_DragStartRow = -1;
		private static int s_DragStartCol = -1;
		private static Rect s_TableRect;
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
		private static readonly Color s_BubbleBg = new Color(0.08f, 0.1f, 0.2f, 0.95f);
		private static readonly Color s_BubbleBorder = new Color(0.3f, 0.5f, 0.9f, 0.8f);
		private static readonly Color s_ApplyBtnColor = new Color(0.15f, 0.4f, 0.15f, 0.9f);
		private static readonly Color s_AddBtnBg = new Color(0.15f, 0.3f, 0.15f, 0.8f);
		private static readonly Color s_AddBtnHover = new Color(0.2f, 0.45f, 0.2f, 0.9f);

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

		// ═══════════════════════════════════════════════════════════════
		//  状态机入口
		// ═══════════════════════════════════════════════════════════════

		private static void UpdateState()
		{
			string focused = GUI.GetNameOfFocusedControl();

			// 检测焦点切换 → 种子编辑文本
			if (focused != s_LastFocusedControl)
			{
				s_LastFocusedControl = focused;
				OnFocusChanged(focused);
			}

			if (string.IsNullOrEmpty(focused))
				s_State = EditorState.TableNavigation;
			else if (focused.StartsWith("XTableColSizeEdit_") || focused.StartsWith("XTableRowSizeEdit_"))
				s_State = EditorState.SizeEditing;
			else if (focused == "XTableSceneEditInput")
				s_State = EditorState.CellEditing;
			else
				s_State = EditorState.TableNavigation;

			s_CurrentControlName = focused;
		}

		private static void OnFocusChanged(string newFocus)
		{
			// 气泡输入框获得焦点：种子为当前尺寸值
			if (newFocus.StartsWith("XTableColSizeEdit_") && s_ActiveTable != null)
			{
				if (int.TryParse(newFocus.Substring("XTableColSizeEdit_".Length), out int idx))
				{
					s_SizeEditIsColumn = true;
					s_SizeEditTargetIndex = idx;
					s_SizeEditText = s_ColWidths != null && idx < s_ColWidths.Length
						? s_ColWidths[idx].ToString("F0") : "";
				}
			}
			else if (newFocus.StartsWith("XTableRowSizeEdit_") && s_ActiveTable != null)
			{
				if (int.TryParse(newFocus.Substring("XTableRowSizeEdit_".Length), out int idx))
				{
					s_SizeEditIsColumn = false;
					s_SizeEditTargetIndex = idx;
					s_SizeEditText = s_RowHeights != null && idx < s_RowHeights.Length
						? s_RowHeights[idx].ToString("F0") : "";
				}
			}
			// 底部内容输入框获得焦点：种子为当前单元格内容
			else if (newFocus == "XTableSceneEditInput" && s_ActiveTable != null)
			{
				SyncEditText(s_ActiveTable);
			}
		}

		/// <summary>将 s_EditText 同步为当前选中单元格的内容（选中变更时调用）</summary>
		private static void SyncEditText(XericUIActionTable table)
		{
			int sr = table.SelectedRow;
			int sc = table.SelectedCol;
			if (sr >= 0 && sc >= 0)
			{
				var data = table.GetCellData(sr, sc);
				s_EditText = (data != null) ? (data.Text ?? "") : "";
			}
			else
			{
				s_EditText = "";
			}
		}

		// ═══════════════════════════════════════════════════════════════
		//  主入口
		// ═══════════════════════════════════════════════════════════════

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

			// 使用 Content 的 RectTransform 进行定位计算（而非表格组件所在的 ScrollRect 根节点）
			RectTransform tableRt = table.ContentTransform.RectTransform();
			if (tableRt == null) tableRt = table.GetComponent<RectTransform>();
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

			// 更新缓存
			s_TableRect = tableRect;
			s_XScale = xScale;
			s_YScale = yScale;
			s_RowHeights = rh;
			s_ColWidths = cw;
			s_ActiveTable = table;

			Handles.BeginGUI();

			// ★ 状态机：必须最先执行，后续所有逻辑依赖 s_State
			UpdateState();

			// Unity 2021 兼容：阻止鼠标事件穿透 overlay 到背后 UI 对象
			// 原理：在 overlay 区域注册默认控件，Scene View 拣选系统检测到控件后
			//       不再选中背后 GameObject（Canvas、ScrollRect 等）
			if (s_State == EditorState.TableNavigation)
			{
				// 覆盖区域：行标签 + 列表头 + 表格主体 + 底部工具栏
				Rect overlayArea = new Rect(
					tableRect.x - COL_W - MARGIN,                    // 包含行标签
					tableRect.y - ROW_H - MARGIN,                    // 包含列表头
					tableRect.width + COL_W + MARGIN + 4,            // 包含行标签
					tableRect.height + ROW_H + MARGIN + 56);         // 包含列表头+工具栏

				if (overlayArea.Contains(Event.current.mousePosition))
				{
					int blockId = GUIUtility.GetControlID(FocusType.Passive);
					HandleUtility.AddDefaultControl(blockId);
				}
			}

			DrawRowLabels(rh, tableRect, xScale, yScale, table);
			DrawColumnLabels(cw, tableRect, xScale, yScale, table);
			DrawGridLines(rh, cw, tableRect, xScale, yScale);
			DrawSelectionHighlight(rh, cw, tableRect, xScale, yScale, table);
			DrawAddButtons(rh, cw, tableRect, xScale, yScale, table);
			DrawSizeEditBubble(rh, cw, tableRect, xScale, yScale, table);
			DrawInputBar(table, tableRect.x, tableRect.y + tableRect.height + MARGIN);

			// 非导航状态不处理鼠标/键盘表格操作
			if (s_State == EditorState.TableNavigation)
			{
				HandleTableMouse(table, rh, cw, tableRect, xScale, yScale);
				HandleKeyboard(table, rh, cw);
			}

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

		private static bool IsRowInMultiSelect(XericUIActionTable table, int row)
		{
			if (!table.HasMultiSelection) return false;
			int minR = Mathf.Min(table.SelectStartRow, table.SelectEndRow);
			int maxR = Mathf.Max(table.SelectStartRow, table.SelectEndRow);
			return row >= minR && row <= maxR;
		}

		private static bool IsColInMultiSelect(XericUIActionTable table, int col)
		{
			if (!table.HasMultiSelection) return false;
			int minC = Mathf.Min(table.SelectStartCol, table.SelectEndCol);
			int maxC = Mathf.Max(table.SelectStartCol, table.SelectEndCol);
			return col >= minC && col <= maxC;
		}

		/// <summary>创建纯色 1x1 纹理，用于按钮背景</summary>
		private static Texture2D MakeTex(int w, int h, Color col)
		{
			Color[] pix = new Color[w * h];
			for (int i = 0; i < pix.Length; i++)
				pix[i] = col;
			Texture2D result = new Texture2D(w, h);
			result.SetPixels(pix);
			result.Apply();
			return result;
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

				// 表格导航状态下才响应鼠标
				if (s_State != EditorState.TableNavigation) { y += cellH; continue; }

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int col = table.SelectedCol;
					if (col < 0) col = 0;

					s_IsDragging = true;
					s_DragStartRow = r;
					s_DragStartCol = col;

					if (Event.current.shift && table.SelectedRow >= 0)
				{
					if (!table.HasMultiSelection)
						table.SelectCell(table.SelectedRow, table.SelectedCol, r, col);
					else
						table.SelectionHandle.ExtendTo(r, col);
				}
				else
				{
					table.SelectCell(r, col);
				}

				table.RefreshView();
				SyncEditText(table);
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

				// 表格导航状态下才响应鼠标
				if (s_State != EditorState.TableNavigation) { x += cellW; continue; }

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int row = table.SelectedRow;
					if (row < 0) row = 0;

					s_IsDragging = true;
					s_DragStartRow = row;
					s_DragStartCol = c;

					if (Event.current.shift && table.SelectedCol >= 0)
				{
					if (!table.HasMultiSelection)
						table.SelectCell(table.SelectedRow, table.SelectedCol, row, c);
					else
						table.SelectionHandle.ExtendTo(row, c);
				}
				else
				{
					table.SelectCell(row, c);
				}

				table.RefreshView();
				SyncEditText(table);
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

			// 水平网格线（跳过合并区域内的线）
			float y = tr.y;
			for (int r = 0; r < rh.Length; r++)
			{
				y += rh[r] * yScale;

				// 检查此行是否在合并区域内（非底部边界的行）
				bool skipLine = false;
				if (s_ActiveTable != null && r + 1 < rh.Length)
				{
					var data = s_ActiveTable.TableData;
					if (data != null && data.IsCellMerged(r + 1, 0))
						skipLine = true;
				}

				if (!skipLine)
					EditorGUI.DrawRect(new Rect(tr.x, y, tr.width, 1), lineColor);
			}

			// 垂直网格线（跳过合并区域内的线）
			float x = tr.x;
			for (int c = 0; c < cw.Length; c++)
			{
				x += cw[c] * xScale;

				// 检查此列是否在合并区域内（非右侧边界的列）
				bool skipLine = false;
				if (s_ActiveTable != null && c + 1 < cw.Length)
				{
					var data = s_ActiveTable.TableData;
					if (data != null && data.IsCellMerged(0, c + 1))
						skipLine = true;
				}

				if (!skipLine)
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

			// 获取选中单元格的实际显示范围（合并源需要扩展）
			int hlRowSpan = 1, hlColSpan = 1;
			table.IsMergeSource(sr, sc, out hlRowSpan, out hlColSpan);

			if (table.HasMultiSelection)
			{
				int minR = Mathf.Min(table.SelectStartRow, table.SelectEndRow);
				int maxR = Mathf.Max(table.SelectStartRow, table.SelectEndRow);
				int minC = Mathf.Min(table.SelectStartCol, table.SelectEndCol);
				int maxC = Mathf.Max(table.SelectStartCol, table.SelectEndCol);

				// 扩展多选区域以包含合并源的全部跨度
				if (table.IsMergeSource(minR, minC, out int msRS, out int msCS))
				{
					maxR = Mathf.Max(maxR, minR + msRS - 1);
					maxC = Mathf.Max(maxC, minC + msCS - 1);
				}

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

			// 单选高亮（合并源用完整跨度）
			float sx = tr.x + SumBefore(cw, sc) * xScale;
			float sy = tr.y + SumBefore(rh, sr) * yScale;
			float sw = SumRange(cw, sc, sc + hlColSpan) * xScale;
			float sh = SumRange(rh, sr, sr + hlRowSpan) * yScale;

			EditorGUI.DrawRect(new Rect(sx + 1, sy + 1, sw - 2, sh - 2),
				s_SelectionColor);
		}

		#endregion

		#region 添加行列按钮

		private static void DrawAddButtons(float[] rh, float[] cw, Rect tr,
			float xScale, float yScale, XericUIActionTable table)
		{
			var plusStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				fontStyle = FontStyle.Bold,
				normal = { textColor = Color.white },
				alignment = TextAnchor.MiddleCenter
			};

			float yEnd = tr.y + tr.height;
			Rect rowBtn = new Rect(tr.x - COL_W - MARGIN, yEnd + MARGIN, COL_W, ROW_H);

			bool rowHover = rowBtn.Contains(Event.current.mousePosition);
			EditorGUI.DrawRect(rowBtn, rowHover ? s_AddBtnHover : s_AddBtnBg);
			GUI.Label(rowBtn, "+", plusStyle);

			if (Event.current.type == EventType.MouseDown &&
				Event.current.button == 0 && rowHover)
			{
				table.AddRow();
				table.RefreshView();
				Event.current.Use();
				SceneView.RepaintAll();
			}

			float xEnd = tr.x + tr.width;
			Rect colBtn = new Rect(xEnd + MARGIN, tr.y - ROW_H - MARGIN, COL_W, ROW_H);

			bool colHover = colBtn.Contains(Event.current.mousePosition);
			EditorGUI.DrawRect(colBtn, colHover ? s_AddBtnHover : s_AddBtnBg);
			GUI.Label(colBtn, "+", plusStyle);

			if (Event.current.type == EventType.MouseDown &&
				Event.current.button == 0 && colHover)
			{
				table.AddColumn();
				table.RefreshView();
				Event.current.Use();
				SceneView.RepaintAll();
			}
		}

		#endregion

		#region 气泡尺寸编辑器（含应用按钮）

		private const float BUBBLE_W = 60;
		private const float BUBBLE_H = 22;
		private const float BUBBLE_APPLY_BTN_W = 24;
		private const float BUBBLE_DEL_BTN_W = 24;

		private static GUIStyle s_BubbleApplyBtnStyle;
		private static GUIStyle s_BubbleApplyBtnStyleActive;
		private static GUIStyle s_BubbleDelBtnStyle;

		private static void EnsureBubbleStyles()
		{
			if (s_BubbleApplyBtnStyle != null) return;
			s_BubbleApplyBtnStyle = new GUIStyle(GUI.skin.button)
			{
				fontSize = 11,
				fontStyle = FontStyle.Bold,
				normal = { textColor = new Color(0.5f, 0.5f, 0.5f), background = MakeTex(1, 1, new Color(0.25f, 0.25f, 0.25f, 0.6f)) },
				hover = { textColor = new Color(0.7f, 0.7f, 0.7f), background = MakeTex(1, 1, new Color(0.35f, 0.35f, 0.35f, 0.8f)) },
				active = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.15f, 0.8f)) },
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0),
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(0, 0, 0, 0)
			};
			s_BubbleApplyBtnStyleActive = new GUIStyle(s_BubbleApplyBtnStyle)
			{
				normal = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.2f, 0.6f, 0.2f, 0.9f)) },
				hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.2f, 0.55f, 0.2f, 0.95f)) },
				active = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.1f, 0.4f, 0.1f, 0.95f)) }
			};
			s_BubbleDelBtnStyle = new GUIStyle(s_BubbleApplyBtnStyle)
			{
				normal = { textColor = new Color(0.6f, 0.3f, 0.3f), background = MakeTex(1, 1, new Color(0.3f, 0.15f, 0.15f, 0.6f)) },
				hover = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.7f, 0.2f, 0.2f, 0.95f)) },
				active = { textColor = Color.white, background = MakeTex(1, 1, new Color(0.5f, 0.1f, 0.1f, 0.95f)) }
			};
		}

		private static void DrawSizeEditBubble(float[] rh, float[] cw, Rect tr,
			float xScale, float yScale, XericUIActionTable table)
		{
			int selRow = table.SelectedRow;
			int selCol = table.SelectedCol;

			// 气泡固定总宽：输入框 + 应用按钮 + 删除按钮 + 间距
			const float totalW = BUBBLE_W + BUBBLE_APPLY_BTN_W + BUBBLE_DEL_BTN_W + 4f;

			// ── 列宽气泡：左对齐列左边缘，位于列标签上方 ──
			if (selCol >= 0 && selCol < cw.Length && selRow < 0)
			{
				float cx = tr.x + SumBefore(cw, selCol) * xScale;
				float bx = cx + 2f;
				float by = tr.y - ROW_H - MARGIN - BUBBLE_H - 4f;
				DrawBubble(table, bx, by, cw[selCol], totalW, isColumn: true, index: selCol);
			}

			// ── 行高气泡：左对齐行标签左侧 ──
			if (selRow >= 0 && selRow < rh.Length && selCol < 0)
			{
				float ry = tr.y + SumBefore(rh, selRow) * yScale;
				float rhScaled = rh[selRow] * yScale;
				float bx = tr.x - COL_W - MARGIN - totalW - 4f;
				float by = ry + (rhScaled - BUBBLE_H) / 2f;
				DrawBubble(table, bx, by, rh[selRow], totalW, isColumn: false, index: selRow);
			}

			// ── 单元格选中时同时显示 ──
			if (selRow >= 0 && selCol >= 0)
			{
				if (selCol < cw.Length)
				{
					float cx = tr.x + SumBefore(cw, selCol) * xScale;
					float bx = cx + 2f;
					float by = tr.y - ROW_H - MARGIN - BUBBLE_H - 4f;
					DrawBubble(table, bx, by, cw[selCol], totalW, isColumn: true, index: selCol);
				}
				if (selRow < rh.Length)
				{
					float ry = tr.y + SumBefore(rh, selRow) * yScale;
					float rhScaled = rh[selRow] * yScale;
					float bx = tr.x - COL_W - MARGIN - totalW - 4f;
					float by = ry + (rhScaled - BUBBLE_H) / 2f;
					DrawBubble(table, bx, by, rh[selRow], totalW, isColumn: false, index: selRow);
				}
			}
		}

		private static void DrawBubble(XericUIActionTable table,
			float x, float y, float currentValue,
			float totalW, bool isColumn, int index)
		{
			string controlName = isColumn
				? $"XTableColSizeEdit_{index}"
				: $"XTableRowSizeEdit_{index}";
			bool isFocused = GUI.GetNameOfFocusedControl() == controlName;

			Rect bubbleRect = new Rect(x, y, totalW, BUBBLE_H);

			EditorGUI.DrawRect(bubbleRect, s_BubbleBg);
			EditorGUI.DrawRect(new Rect(x, y, totalW, 1), s_BubbleBorder);
			EditorGUI.DrawRect(new Rect(x, y + BUBBLE_H - 1, totalW, 1), s_BubbleBorder);
			EditorGUI.DrawRect(new Rect(x, y, 1, BUBBLE_H), s_BubbleBorder);
			EditorGUI.DrawRect(new Rect(x + totalW - 1, y, 1, BUBBLE_H), s_BubbleBorder);

			var inputStyle = new GUIStyle(GUI.skin.textField)
			{
				fontSize = 11,
				margin = new RectOffset(1, 1, 1, 1),
				alignment = TextAnchor.MiddleCenter
			};

			// 输入框占左
			Rect inputRect = new Rect(x + 2, y + 2, BUBBLE_W - 4, BUBBLE_H - 4);
			GUI.SetNextControlName(controlName);

			if (isFocused)
				s_SizeEditText = GUI.TextField(inputRect, s_SizeEditText, inputStyle);
			else
				GUI.TextField(inputRect, currentValue.ToString("F0"), inputStyle);

			// 应用按钮 — 聚焦时绿色，否则灰色
			EnsureBubbleStyles();
			Rect applyBtnRect = new Rect(x + BUBBLE_W + 1f, y + 1, BUBBLE_APPLY_BTN_W, BUBBLE_H - 2);
			var btnStyle = isFocused ? s_BubbleApplyBtnStyleActive : s_BubbleApplyBtnStyle;

			if (GUI.Button(applyBtnRect, "✓", btnStyle))
			{
				ApplyBubbleEdit(table, isColumn, index);
				GUI.FocusControl(null);
			}

			// 删除按钮 — 删除当前行/列
			Rect delBtnRect = new Rect(x + BUBBLE_W + BUBBLE_APPLY_BTN_W + 2f, y + 1, BUBBLE_DEL_BTN_W, BUBBLE_H - 2);
			if (GUI.Button(delBtnRect, "✕", s_BubbleDelBtnStyle))
			{
				ApplyBubbleDelete(table, isColumn, index);
			}

			// 回车提交
			if (isFocused &&
				Event.current.type == EventType.KeyDown &&
				Event.current.keyCode == KeyCode.Return)
			{
				ApplyBubbleEdit(table, isColumn, index);
				Event.current.Use();
			}
		}

		private static void ApplyBubbleEdit(XericUIActionTable table, bool isColumn, int index)
		{
			string trimmed = s_SizeEditText.Trim();
			if (float.TryParse(trimmed, out float value) && value > 0)
			{
				if (isColumn)
					table.SetColWidth(index, value);
				else
					table.SetRowHeight(index, value);
				table.RefreshView();
			}
			GUI.FocusControl(null);
			s_State = EditorState.TableNavigation;
			SceneView.RepaintAll();
		}

		private static void ApplyBubbleDelete(XericUIActionTable table, bool isColumn, int index)
		{
			if (isColumn)
				table.RemoveColumn(index);
			else
				table.RemoveRow(index);

			GUI.FocusControl(null);
			s_State = EditorState.TableNavigation;
			SceneView.RepaintAll();
		}

		#endregion

		#region 底部输入框

		private static void DrawInputBar(XericUIActionTable table, float x, float y)
		{
			const float barW = 450;
			const float barH = 52;
			const float rowH = 22;
			const float pad = 2;

			Rect barRect = new Rect(x, y, barW, barH);
			EditorGUI.DrawRect(barRect, s_InputBg);

			if (table.SelectedRow >= 0 && table.SelectedCol >= 0)
			{
				var labelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					normal = { textColor = s_LabelText },
					alignment = TextAnchor.MiddleCenter
				};
				var inputStyle = new GUIStyle(GUI.skin.textField)
				{
					fontSize = 12,
					margin = new RectOffset(1, 1, 1, 1)
				};
				var btnStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 11,
					margin = new RectOffset(1, 1, 1, 1),
					padding = new RectOffset(4, 4, 2, 2)
				};

				// Row 1: 坐标标签 | 输入框 | 应用按钮 + 合并/拆分按钮
				float actionBtnW = 50;
				float row1Y = barRect.y + pad;
				float row2Y = barRect.y + rowH + pad + 4;
				string cellLabel = table.GetSelectionText();

				// 合并按钮（多选时显示）
				bool showMerge = table.HasMultiSelection &&
				                 (table.SelectStartRow != table.SelectEndRow ||
				                  table.SelectStartCol != table.SelectEndCol);

				// 拆分按钮（选中单元格是合并源时显示）
				bool isMergeSource = table.IsMergeSource(table.SelectedRow,
					table.SelectedCol, out _, out _);

				float mergeBtnW = showMerge || isMergeSource ? actionBtnW : 0;
				float unmergeBtnW = isMergeSource ? actionBtnW : 0;

				// 坐标标签
				GUI.Label(new Rect(barRect.x + 4, row1Y, 60, rowH), cellLabel, labelStyle);

				// 输入框
				float inputW = barW - 68 - 44 - 2 - mergeBtnW - unmergeBtnW - pad * 2 - 4;
				Rect inputRect = new Rect(barRect.x + 68, row1Y, inputW, rowH);

				if (s_State == EditorState.CellEditing &&
					Event.current.type == EventType.KeyDown &&
					Event.current.keyCode == KeyCode.Return)
				{
					ApplyCellEdit(table);
					Event.current.Use();
				}

				GUI.SetNextControlName("XTableSceneEditInput");
				s_EditText = GUI.TextField(inputRect, s_EditText, inputStyle);

				// 应用按钮
				float rightX = barRect.x + barW - pad - 44;
				if (GUI.Button(new Rect(rightX, row1Y, 44, rowH), "应用", btnStyle))
				{
					GUI.FocusControl(null);
					ApplyCellEdit(table);
				}

				// 合并按钮
				if (showMerge)
				{
					var mergeStyle = new GUIStyle(btnStyle)
					{
						normal = { background = MakeTex(1, 1, new Color(0.15f, 0.3f, 0.65f, 0.8f)), textColor = Color.white },
						hover = { background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.85f, 0.9f)), textColor = Color.white }
					};
					if (GUI.Button(new Rect(rightX - mergeBtnW - 2, row1Y, mergeBtnW, rowH), "合并", mergeStyle))
					{
						int minR = Mathf.Min(table.SelectStartRow, table.SelectEndRow);
						int maxR = Mathf.Max(table.SelectStartRow, table.SelectEndRow);
						int minC = Mathf.Min(table.SelectStartCol, table.SelectEndCol);
						int maxC = Mathf.Max(table.SelectStartCol, table.SelectEndCol);
						table.MergeCells(minR, minC, maxR - minR + 1, maxC - minC + 1);
						table.RefreshView();
						SceneView.RepaintAll();
					}
				}

				// 拆分按钮
				if (isMergeSource)
				{
					var unmergeStyle = new GUIStyle(btnStyle)
					{
						normal = { background = MakeTex(1, 1, new Color(0.65f, 0.25f, 0.15f, 0.8f)), textColor = Color.white },
						hover = { background = MakeTex(1, 1, new Color(0.85f, 0.35f, 0.2f, 0.9f)), textColor = Color.white }
					};
					float unmergeX = showMerge ? rightX - mergeBtnW - unmergeBtnW - 4 : rightX - unmergeBtnW - 2;
					if (GUI.Button(new Rect(unmergeX, row1Y, unmergeBtnW, rowH), "拆分", unmergeStyle))
					{
						table.UnmergeCells(table.SelectedRow, table.SelectedCol);
						table.RefreshView();
						SceneView.RepaintAll();
					}
				}

				// Row 2: 样式命名空间下拉框
				var nsLabelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 10,
					normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
					alignment = TextAnchor.MiddleRight
				};
				GUI.Label(new Rect(barRect.x + 4, row2Y, 36, rowH), "样式:", nsLabelStyle);

				string currentNS = table.GetCellStyleNamespace(
					table.SelectedRow, table.SelectedCol) ?? "";

				var namespaces = StyleManager.GetAvailableNamespaces();
				int currentIdx = namespaces.IndexOf(currentNS);
				if (currentIdx < 0) currentIdx = 0;

				Rect dropRect = new Rect(barRect.x + 44, row2Y, barW - 44 - pad, rowH);
				int newIdx = UnityEditor.EditorGUI.Popup(dropRect, currentIdx,
					namespaces.ToArray(), UnityEditor.EditorStyles.popup);

				if (newIdx != currentIdx && newIdx >= 0 && newIdx < namespaces.Count)
				{
					table.SetCellStyleNamespace(table.SelectedRow, table.SelectedCol,
						namespaces[newIdx]);
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
				GUI.Label(new Rect(barRect.x, barRect.y, barW, barH / 2),
					"点击左侧行号或上方列名选中单元格 | Shift+方向键多选", hintStyle);
			}
		}

		private static void ApplyCellEdit(XericUIActionTable table)
		{
			if (table.SelectedRow < 0 || table.SelectedCol < 0) return;
			table.SetCellText(table.SelectedRow, table.SelectedCol, s_EditText);
			table.RefreshView();
			s_State = EditorState.TableNavigation;
			SceneView.RepaintAll();
		}

		#endregion

		#region 表格区域鼠标（仅 TableNavigation 状态调用）

		private static void HandleTableMouse(XericUIActionTable table,
			float[] rh, float[] cw, Rect tr,
			float xScale, float yScale)
		{
			Event e = Event.current;

			switch (e.type)
			{
				case EventType.MouseDown:
					if (e.button != 0) break;
					if (!tr.Contains(e.mousePosition)) break;

					CellFromMouse(tr, xScale, yScale, out int mdRow, out int mdCol);
					if (mdRow < 0 || mdCol < 0) break;

					s_IsDragging = true;
					s_DragStartRow = mdRow;
					s_DragStartCol = mdCol;

					if (e.shift && table.SelectedRow >= 0 && table.SelectedCol >= 0)
				{
					if (!table.HasMultiSelection)
						table.SelectCell(table.SelectedRow, table.SelectedCol, mdRow, mdCol);
					else
						table.SelectionHandle.ExtendTo(mdRow, mdCol);
				}
				else
				{
					table.SelectCell(mdRow, mdCol);
				}

					table.RefreshView();
					SyncEditText(table);
					e.Use();
					SceneView.RepaintAll();
					break;

				case EventType.MouseDrag:
					if (!s_IsDragging || e.button != 0) break;

					CellFromMouse(tr, xScale, yScale, out int dragRow, out int dragCol);
					if (dragRow < 0 || dragCol < 0) break;

					table.SelectionHandle.ExtendTo(dragRow, dragCol);
					table.RefreshView();
					SyncEditText(table);
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

			// 点击合并覆盖的单元格时，重定向到合并源
			if (row >= 0 && col >= 0 && s_ActiveTable != null)
				RedirectToMergeSource(ref row, ref col);
		}

		/// <summary>如果指定单元格被合并覆盖，重定向到合并源（锚点）</summary>
		private static void RedirectToMergeSource(ref int row, ref int col)
		{
			var data = s_ActiveTable.TableData;
			if (data == null) return;

			XTableCoordinateUtility.CellToBlockCoordinate(
				row, col, data.BlockSizeX, data.BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);

			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);
			if (!data.BlockMap.TryGetValue(zIndex, out XTableBlock block))
				return;

			var merge = block.GetMergeRedirect(localRow, localCol);
			if (merge == null) return;

			merge.GetRedirectTarget(out ulong targetBlockIndex, out int targetLocalRow, out int targetLocalCol);

			if (merge.IsCrossBlock && data.BlockMap.TryGetValue(targetBlockIndex, out XTableBlock targetBlock))
			{
				// 跨块合并：从目标块的块坐标反算全局坐标
				XTableBlockMapping.ZIndexToBlockCoord(targetBlockIndex,
					out int targetBlockRow, out int targetBlockCol);
				row = targetBlockRow * data.BlockSizeY + targetLocalRow;
				col = targetBlockCol * data.BlockSizeX + targetLocalCol;
			}
			else
			{
				// 本地合并：源在同一块内，反算全局坐标
				row = blockRow * data.BlockSizeY + targetLocalRow;
				col = blockCol * data.BlockSizeX + targetLocalCol;
			}
		}

		#endregion

		#region 键盘（仅 TableNavigation 状态调用）

		private static void HandleKeyboard(XericUIActionTable table, float[] rh, float[] cw)
		{
			Event e = Event.current;
			if (e.type != EventType.KeyDown) return;

			// Escape 清除选区
			if (e.keyCode == KeyCode.Escape)
			{
				table.ClearSelection();
				table.RefreshView();
				s_EditText = "";
				SyncEditText(table);
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
				SyncEditText(table);
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
				table.SelectCell(table.SelectedRow, table.SelectedCol, row, col);
			else
				table.SelectionHandle.ExtendTo(row, col);
		}
		else
		{
			RedirectToMergeSource(ref row, ref col);
			table.SelectCell(row, col);
		}

			table.RefreshView();
			SyncEditText(table);
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
