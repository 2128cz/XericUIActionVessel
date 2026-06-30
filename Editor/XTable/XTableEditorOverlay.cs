#if UNITY_EDITOR

using UnityEditor;

using UnityEngine;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.XTable.Rendering.Component;

namespace XericUIEditor.XTable
{
	/// <summary>
	/// 场景视图表格编辑覆盖工具——选中表格组件后，在世界编辑器视口中显示网格线和编辑辅助 UI。
	/// 包含合并工具、拖拽引用工具、单元格坐标提示等。
	/// </summary>
	public static class XTableEditorOverlay
	{
		/// <summary>是否启用覆盖层（可通过菜单开关）</summary>
		private static bool s_Enabled = true;

		private const string MENU_PATH = "Xeric UI/表格编辑器覆盖层";

		[MenuItem(MENU_PATH, false, 200)]
		private static void ToggleOverlay()
		{
			s_Enabled = !s_Enabled;
			Menu.SetChecked(MENU_PATH, s_Enabled);
			SceneView.RepaintAll();
		}

		[MenuItem(MENU_PATH, true)]
		private static bool ToggleOverlayValidate()
		{
			Menu.SetChecked(MENU_PATH, s_Enabled);
			return true;
		}

		/// <summary>
		/// 在 Scene View 中绘制表格编辑辅助线。
		/// 调用时机：挂到 SceneView.duringSceneGui 事件上。
		/// 示例：SceneView.duringSceneGui += OnSceneGUI;
		/// </summary>
		public static void OnSceneGUI(SceneView sceneView)
		{
			if (!s_Enabled) return;

			// 获取当前选中的表格组件
			GameObject selected = Selection.activeGameObject;
			if (selected == null) return;

			var table = selected.GetComponent<XericUIActionTable>();
			if (table == null) return;

			var rect = table.RectTransform();
			if (rect == null) return;

			DrawGridOverlay(table, rect);
			DrawCellInfo(table, rect, sceneView);
		}

		/// <summary>绘制网格线</summary>
		private static void DrawGridOverlay(XericUIActionTable table, RectTransform rect)
		{
			float[] rowHeights = table.RowHeights;
			float[] colWidths = table.ColWidths;
			if (rowHeights == null || colWidths == null) return;

			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);

			Vector3 topLeft = corners[1];
			float totalWidth = rect.rect.width;
			float totalHeight = rect.rect.height;

			Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

			// 绘制行分割线
			float accumulatedY = 0f;
			for (int r = 0; r < rowHeights.Length && accumulatedY < totalHeight; r++)
			{
				float y = topLeft.y - accumulatedY - rowHeights[r];
				Vector3 start = new Vector3(topLeft.x, y, topLeft.z);
				Vector3 end = new Vector3(topLeft.x + Mathf.Min(totalWidth, 500f), y, topLeft.z);
				Handles.DrawLine(start, end);
				accumulatedY += rowHeights[r];
			}

			// 绘制列分割线
			float accumulatedX = 0f;
			for (int c = 0; c < colWidths.Length && accumulatedX < totalWidth; c++)
			{
				float x = topLeft.x + accumulatedX + colWidths[c];
				Vector3 start = new Vector3(x, topLeft.y, topLeft.z);
				Vector3 end = new Vector3(x, topLeft.y - Mathf.Min(totalHeight, 500f), topLeft.z);
				Handles.DrawLine(start, end);
				accumulatedX += colWidths[c];
			}
		}

		/// <summary>绘制单元格坐标提示</summary>
		private static void DrawCellInfo(XericUIActionTable table, RectTransform rect, SceneView sceneView)
		{
			int selRow = table.SelectedRow;
			int selCol = table.SelectedCol;
			if (selRow < 0 || selCol < 0) return;

			float[] rowHeights = table.RowHeights;
			float[] colWidths = table.ColWidths;
			if (rowHeights == null || colWidths == null) return;

			// 计算选中单元格的世界位置
			float cellX = 0f;
			for (int c = 0; c < selCol && c < colWidths.Length; c++)
				cellX += colWidths[c];

			float cellY = 0f;
			for (int r = 0; r < selRow && r < rowHeights.Length; r++)
				cellY += rowHeights[r];

			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			Vector3 topLeft = corners[1];

			Vector3 cellCenter = new Vector3(
				topLeft.x + cellX + (selCol < colWidths.Length ? colWidths[selCol] * 0.5f : 0),
				topLeft.y - cellY - (selRow < rowHeights.Length ? rowHeights[selRow] * 0.5f : 0),
				topLeft.z);

			// 绘制选中高亮框
			Handles.color = new Color(0.2f, 0.6f, 1f, 0.5f);
			float cellW = selCol < colWidths.Length ? colWidths[selCol] : 50f;
			float cellH = selRow < rowHeights.Length ? rowHeights[selRow] : 30f;
			Vector3 cellTL = new Vector3(cellCenter.x - cellW * 0.5f, cellCenter.y + cellH * 0.5f, cellCenter.z);
			Vector3 cellTR = new Vector3(cellCenter.x + cellW * 0.5f, cellCenter.y + cellH * 0.5f, cellCenter.z);
			Vector3 cellBR = new Vector3(cellCenter.x + cellW * 0.5f, cellCenter.y - cellH * 0.5f, cellCenter.z);
			Vector3 cellBL = new Vector3(cellCenter.x - cellW * 0.5f, cellCenter.y - cellH * 0.5f, cellCenter.z);
			Handles.DrawSolidRectangleWithOutline(
				new Vector3[] { cellTL, cellTR, cellBR, cellBL },
				new Color(0.2f, 0.6f, 1f, 0.2f),
				new Color(0.2f, 0.6f, 1f, 0.8f));

			// 显示单元格坐标标签
			Handles.Label(cellCenter + Vector3.up * 10f,
				$"({selRow}, {selCol})",
				new GUIStyle(EditorStyles.label)
				{
					normal = { textColor = Color.white },
					fontStyle = FontStyle.Bold
				});

			// 工具栏按钮（在 Scene View 顶部绘制）
			Handles.BeginGUI();
			GUILayout.BeginArea(new Rect(10, 10, 200, 80));
			GUILayout.BeginVertical("表格编辑工具", GUI.skin.window);

			if (GUILayout.Button("合并选区"))
			{
				Debug.Log($"[XTable] 合并起始于 ({selRow}, {selCol})");
			}

			if (GUILayout.Button("取消合并"))
			{
				Debug.Log($"[XTable] 取消合并 ({selRow}, {selCol})");
			}

			GUILayout.EndVertical();
			GUILayout.EndArea();
			Handles.EndGUI();
		}
	}
}

#endif
