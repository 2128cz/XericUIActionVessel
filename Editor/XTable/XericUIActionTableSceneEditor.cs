using UnityEditor;

using UnityEngine;

using XericUI.XTable.Rendering.Component;

namespace XericUIEditor.XTable
{
	/// <summary>
	/// Scene View 表格编辑覆盖层——
	/// 左侧行索引、顶部列索引、底部输入框。
	/// 点击行列交叉区域选中单元格，底部输入框编辑文本。
	/// 不创建任何场景对象，通过 Handles.BeginGUI/EndGUI 绘制。
	/// </summary>
	[InitializeOnLoad]
	public static class XericUIActionTableSceneEditor
	{
		private static bool s_Enabled;
		private const string ENABLED_KEY = "XericUI_TableSceneEditor_Enabled";

		private static string s_EditText = "";

		private static readonly Color s_LabelBg = new Color(0.12f, 0.12f, 0.18f, 0.9f);
		private static readonly Color s_SelectedLabel = new Color(0.2f, 0.5f, 0.9f, 0.6f);
		private static readonly Color s_LabelText = new Color(0.75f, 0.75f, 0.8f);
		private static readonly Color s_InputBg = new Color(0.08f, 0.08f, 0.14f, 0.92f);

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

			// 不管选了表格祖上还是子孙，都能找到组件
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

			// GetWorldCorners 返回世界坐标，topleft = corners[1]
			Vector3[] corners = new Vector3[4];
			tableRt.GetWorldCorners(corners);

			Camera svCam = sceneView.camera;
			Rect camRect = svCam.pixelRect;

			// 对 ScreenSpaceOverlay Canvas，世界坐标即屏幕像素坐标
			// 需要映射到 SceneView 本地 GUI 坐标
			Vector2 guiTL = ScreenToSceneViewGUI(corners[1], camRect);
			Vector2 guiBR = ScreenToSceneViewGUI(corners[3], camRect);

			float tableW = guiBR.x - guiTL.x;
			float tableH = guiBR.y - guiTL.y;
			if (tableW <= 0 || tableH <= 0) return;

			float totalW = 0, totalH = 0;
			foreach (float w in cw) totalW += w;
			foreach (float h in rh) totalH += h;
			if (totalW <= 0 || totalH <= 0) return;

			float xScale = tableW / totalW;
			float yScale = tableH / totalH;

			// 表格区域（GUI 坐标，Y 向下）
			Rect tableRect = new Rect(guiTL.x, guiTL.y, tableW, tableH);

			Handles.BeginGUI();

			DrawRowLabels(rh, tableRect, xScale, yScale, table);
			DrawColumnLabels(cw, tableRect, xScale, yScale, table);
			DrawGridLines(rh, cw, tableRect, xScale, yScale);
			DrawSelectionHighlight(rh, cw, tableRect, xScale, yScale, table);
			DrawInputBar(table, tableRect.x, tableRect.y + tableRect.height + MARGIN);
			HandleInput(table, rh, cw, tableRect, xScale, yScale);

			Handles.EndGUI();
		}

		/// <summary>屏幕坐标转 SceneView 本地 GUI 坐标</summary>
		private static Vector2 ScreenToSceneViewGUI(Vector2 screenPos, Rect camPixelRect)
		{
			return new Vector2(
				screenPos.x - camPixelRect.x,
				camPixelRect.yMax - screenPos.y
			);
		}

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

				bool isSelected = (table.SelectedRow == r);
				EditorGUI.DrawRect(labelRect, isSelected ? s_SelectedLabel : s_LabelBg);
				GUI.Label(labelRect, $"{r}", style);

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int col = table.SelectedCol;
					if (col < 0) col = 0;
					table.SelectCell(r, col);
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

				bool isSelected = (table.SelectedCol == c);
				EditorGUI.DrawRect(labelRect, isSelected ? s_SelectedLabel : s_LabelBg);
				string text = cellW > 40 ? $"列{c}" : (cellW > 18 ? $"{c}" : "");
				GUI.Label(labelRect, text, style);

				if (Event.current.type == EventType.MouseDown &&
					Event.current.button == 0 &&
					labelRect.Contains(Event.current.mousePosition))
				{
					int row = table.SelectedRow;
					if (row < 0) row = 0;
					table.SelectCell(row, c);
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

			float sx = tr.x;
			for (int c = 0; c < sc; c++) sx += cw[c] * xScale;

			float sy = tr.y;
			for (int r = 0; r < sr; r++) sy += rh[r] * yScale;

			float sw = cw[sc] * xScale;
			float sh = rh[sr] * yScale;

			EditorGUI.DrawRect(new Rect(sx + 1, sy + 1, sw - 2, sh - 2),
				new Color(0.18f, 0.45f, 0.85f, 0.25f));
		}

		#endregion

		#region 底部输入框

		private static void DrawInputBar(XericUIActionTable table, float x, float y)
		{
			Rect barRect = new Rect(x, y, 400, 24);
			EditorGUI.DrawRect(barRect, s_InputBg);

			if (table.SelectedRow >= 0 && table.SelectedCol >= 0)
			{
				var labelStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					normal = { textColor = s_LabelText },
					alignment = TextAnchor.MiddleCenter
				};
				GUI.Label(new Rect(barRect.x + 4, barRect.y, 50, barRect.height),
					$"({table.SelectedRow},{table.SelectedCol})", labelStyle);

				var inputStyle = new GUIStyle(GUI.skin.textField)
				{
					fontSize = 12,
					margin = new RectOffset(2, 2, 2, 2)
				};
				Rect inputRect = new Rect(barRect.x + 56, barRect.y + 2, 180, barRect.height - 4);
				s_EditText = GUI.TextField(inputRect, s_EditText, inputStyle);

				var btnStyle = new GUIStyle(GUI.skin.button)
				{
					fontSize = 11,
					margin = new RectOffset(2, 2, 2, 2),
					padding = new RectOffset(4, 4, 2, 2)
				};
				if (GUI.Button(new Rect(barRect.x + 242, barRect.y + 2, 50, barRect.height - 4),
					"应用", btnStyle))
				{
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
				GUI.Label(barRect, "点击左侧行号或上方列号选中单元格", hintStyle);
			}
		}

		private static void ApplyEdit(XericUIActionTable table)
		{
			if (table.SelectedRow < 0 || table.SelectedCol < 0) return;
			table.SetCellText(table.SelectedRow, table.SelectedCol, s_EditText);
			table.RefreshView();
			SceneView.RepaintAll();
		}

		private static void UpdateEditText(XericUIActionTable table, int row, int col)
		{
			var data = table.GetCellData(row, col);
			s_EditText = data != null ? data.Text ?? "" : "";
		}

		#endregion

		#region 表格区域点击

		private static void HandleInput(XericUIActionTable table,
			float[] rh, float[] cw, Rect tr,
			float xScale, float yScale)
		{
			Event e = Event.current;
			if (e.type != EventType.MouseDown || e.button != 0) return;
			if (!tr.Contains(e.mousePosition)) return;

			float rx = (e.mousePosition.x - tr.x) / xScale;
			float ry = (e.mousePosition.y - tr.y) / yScale;

			int col = IndexFromCoord(rx, cw);
			int row = IndexFromCoord(ry, rh);
			if (row < 0 || col < 0) return;

			table.SelectCell(row, col);
			table.RefreshView();
			UpdateEditText(table, row, col);

			e.Use();
			SceneView.RepaintAll();
		}

		#endregion

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
	}
}
