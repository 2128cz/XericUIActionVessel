using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using XericUI.XTable.Rendering.Component;

namespace XericUIEditor.XTable
{
	/// <summary>
	/// XericUIActionTable 的 Inspector 编辑器——
	/// 折叠分组：[样式设定] [尺寸设定] [编辑测试] [数据浏览]。
	/// 使用 EditorGUILayout.Foldout 避免与数组 PropertyField 内部的折叠冲突。
	/// </summary>
	[CustomEditor(typeof(XericUIActionTable))]
	public class XericUIActionTableEditor : Editor
	{
		private XericUIActionTable m_Table;

		private SerializedProperty m_RowHeightsProp;
		private SerializedProperty m_ColWidthsProp;
		private SerializedProperty m_DefaultStyleNsProp;
		private SerializedProperty m_ScrollRectProp;

		// 折叠状态
		private static bool s_StyleFoldout = true;
		private static bool s_SizeFoldout = true;
		private static bool s_EditTestFoldout;
		private static bool s_DataBrowseFoldout;

		// 编辑测试参数
		private int m_BatchRowCount = 10;
		private int m_BatchColCount = 10;
		private float m_BatchCellHeight = 40f;
		private float m_BatchCellWidth = 100f;

		private int m_MergeStartRow;
		private int m_MergeStartCol;
		private int m_MergeRowSpan = 2;
		private int m_MergeColSpan = 2;

		private int m_EditRow;
		private int m_EditCol;
		private string m_EditText = "";

		private void OnEnable()
		{
			m_Table = (XericUIActionTable)target;

			m_RowHeightsProp = serializedObject.FindProperty("m_RowHeights");
			m_ColWidthsProp = serializedObject.FindProperty("m_ColWidths");
			m_DefaultStyleNsProp = serializedObject.FindProperty("m_DefaultStyleNamespace");
			m_ScrollRectProp = serializedObject.FindProperty("m_ScrollRect");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// ===== 样式设定 =====
			s_StyleFoldout = EditorGUILayout.Foldout(s_StyleFoldout, "样式设定", true);
			if (s_StyleFoldout)
			{
				EditorGUI.indentLevel++;
				if (m_DefaultStyleNsProp != null)
					EditorGUILayout.PropertyField(m_DefaultStyleNsProp, new GUIContent("默认命名空间"));
				if (m_ScrollRectProp != null)
					EditorGUILayout.PropertyField(m_ScrollRectProp, new GUIContent("ScrollRect"));
				EditorGUI.indentLevel--;
			}

			// ===== 尺寸设定 =====
			EditorGUILayout.Space(4);
			s_SizeFoldout = EditorGUILayout.Foldout(s_SizeFoldout, "尺寸设定", true);
			if (s_SizeFoldout)
			{
				EditorGUI.indentLevel++;
				if (m_RowHeightsProp != null)
					EditorGUILayout.PropertyField(m_RowHeightsProp, new GUIContent("行高"), true);
				if (m_ColWidthsProp != null)
					EditorGUILayout.PropertyField(m_ColWidthsProp, new GUIContent("列宽"), true);
				EditorGUI.indentLevel--;
			}

			// ===== 编辑测试 =====
			EditorGUILayout.Space(4);
			s_EditTestFoldout = EditorGUILayout.Foldout(s_EditTestFoldout, "编辑测试", true);
			if (s_EditTestFoldout)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.LabelField("批量生成尺寸", EditorStyles.miniBoldLabel);
				m_BatchRowCount = EditorGUILayout.IntField("行数", m_BatchRowCount);
				m_BatchColCount = EditorGUILayout.IntField("列数", m_BatchColCount);
				m_BatchCellHeight = EditorGUILayout.FloatField("单元格高度", m_BatchCellHeight);
				m_BatchCellWidth = EditorGUILayout.FloatField("单元格宽度", m_BatchCellWidth);
				if (GUILayout.Button("应用批量尺寸", GUILayout.Height(22)))
					ApplyBatchRowColSizes();

				EditorGUILayout.Space(6);

				EditorGUILayout.LabelField("合并单元格", EditorStyles.miniBoldLabel);
				m_MergeStartRow = EditorGUILayout.IntField("起始行", m_MergeStartRow);
				m_MergeStartCol = EditorGUILayout.IntField("起始列", m_MergeStartCol);
				m_MergeRowSpan = EditorGUILayout.IntField("行跨度", m_MergeRowSpan);
				m_MergeColSpan = EditorGUILayout.IntField("列跨度", m_MergeColSpan);
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("合并", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.MergeCells(m_MergeStartRow, m_MergeStartCol, m_MergeRowSpan, m_MergeColSpan);
				if (GUILayout.Button("取消合并", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.UnmergeCells(m_MergeStartRow, m_MergeStartCol);
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(6);

				EditorGUILayout.LabelField("单元格编辑", EditorStyles.miniBoldLabel);
				m_EditRow = EditorGUILayout.IntField("行", m_EditRow);
				m_EditCol = EditorGUILayout.IntField("列", m_EditCol);

				var existing = m_Table.GetCellData(m_EditRow, m_EditCol);
				if (existing != null && !string.IsNullOrEmpty(existing.Text) && string.IsNullOrEmpty(m_EditText))
					m_EditText = existing.Text;

				m_EditText = EditorGUILayout.TextField("文本内容", m_EditText);

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("应用文本", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.SetCellText(m_EditRow, m_EditCol, m_EditText);
				if (GUILayout.Button("选中此格", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.SelectCell(m_EditRow, m_EditCol);
				if (GUILayout.Button("清除选中", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.ClearSelection();
				EditorGUILayout.EndHorizontal();

				EditorGUI.indentLevel--;
			}

			// ===== 数据浏览 =====
			EditorGUILayout.Space(4);
			s_DataBrowseFoldout = EditorGUILayout.Foldout(s_DataBrowseFoldout, "数据浏览", true);
			if (s_DataBrowseFoldout)
			{
				EditorGUI.indentLevel++;

				if (m_Table.TableData != null)
				{
					EditorGUI.BeginDisabledGroup(true);
					EditorGUILayout.IntField("块数量", m_Table.TableData.BlockCount);
					EditorGUILayout.TextField("块尺寸",
						$"{m_Table.TableData.BlockSizeX} × {m_Table.TableData.BlockSizeY}");
					EditorGUILayout.TextField("选中单元格",
						m_Table.SelectedRow >= 0 ? $"({m_Table.SelectedRow}, {m_Table.SelectedCol})" : "(未选中)");
					EditorGUI.EndDisabledGroup();
				}
				else
				{
					EditorGUILayout.HelpBox("TableData 为空——Awake() 执行后会自动创建默认实例。", MessageType.Info);
				}

				EditorGUILayout.Space(4);
				if (GUILayout.Button("手动刷新视图", GUILayout.Height(22)))
					EditorApplication.delayCall += () => m_Table.RefreshView();

				EditorGUI.indentLevel--;
			}

			serializedObject.ApplyModifiedProperties();
		}

		private void ApplyBatchRowColSizes()
		{
			serializedObject.Update();

			if (m_RowHeightsProp != null)
			{
				m_RowHeightsProp.arraySize = m_BatchRowCount;
				for (int i = 0; i < m_BatchRowCount; i++)
					m_RowHeightsProp.GetArrayElementAtIndex(i).floatValue = m_BatchCellHeight;
			}

			if (m_ColWidthsProp != null)
			{
				m_ColWidthsProp.arraySize = m_BatchColCount;
				for (int i = 0; i < m_BatchColCount; i++)
					m_ColWidthsProp.GetArrayElementAtIndex(i).floatValue = m_BatchCellWidth;
			}

			serializedObject.ApplyModifiedProperties();
		}

		#region Hierarchy 创建菜单

		[MenuItem("GameObject/Xeric UI/Table/Xeric UI Action Table", false, 60)]
		private static void CreateTable(MenuCommand menuCommand)
		{
			// 1. 创建表格内容节点
			GameObject contentGo = new GameObject("Content",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(XericUIActionTable));
			RectTransform contentRt = contentGo.GetComponent<RectTransform>();
			contentRt.anchorMin = new Vector2(0, 1);
			contentRt.anchorMax = new Vector2(0, 1);
			contentRt.pivot = new Vector2(0, 1);
			contentRt.anchoredPosition = Vector2.zero;
			contentRt.sizeDelta = new Vector2(800, 600);
			XericUIActionTable table = contentGo.GetComponent<XericUIActionTable>();

			// 2. 创建 Viewport（Mask 裁剪区域）
			GameObject viewportGo = new GameObject("Viewport",
				typeof(RectTransform), typeof(Image), typeof(Mask));
			viewportGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
			RectTransform vpRt = viewportGo.GetComponent<RectTransform>();
			vpRt.anchorMin = Vector2.zero;
			vpRt.anchorMax = Vector2.one;
			vpRt.sizeDelta = Vector2.zero;
			contentRt.SetParent(vpRt, false);

			// 3. 创建水平滚动条
			GameObject hScrollbarGo = new GameObject("Scrollbar Horizontal",
				typeof(RectTransform), typeof(Scrollbar), typeof(Image));
			RectTransform hBarRt = hScrollbarGo.GetComponent<RectTransform>();
			hBarRt.anchorMin = new Vector2(0, 0);
			hBarRt.anchorMax = new Vector2(1, 0);
			hBarRt.pivot = new Vector2(0.5f, 0);
			hBarRt.anchoredPosition = new Vector2(0, 4);
			hBarRt.sizeDelta = new Vector2(-16, 20);
			Image hBarImg = hScrollbarGo.GetComponent<Image>();
			hBarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
			Scrollbar hBar = hScrollbarGo.GetComponent<Scrollbar>();
			hBar.direction = Scrollbar.Direction.LeftToRight;

			GameObject hSlidingGo = new GameObject("Sliding Area",
				typeof(RectTransform));
			hSlidingGo.transform.SetParent(hScrollbarGo.transform, false);
			RectTransform hSlidingRt = hSlidingGo.GetComponent<RectTransform>();
			hSlidingRt.anchorMin = Vector2.zero;
			hSlidingRt.anchorMax = Vector2.one;
			hSlidingRt.sizeDelta = new Vector2(-20, -20);

			GameObject hHandleGo = new GameObject("Handle",
				typeof(RectTransform), typeof(Image));
			hHandleGo.transform.SetParent(hSlidingGo.transform, false);
			RectTransform hHandleRt = hHandleGo.GetComponent<RectTransform>();
			hHandleRt.anchorMin = Vector2.zero;
			hHandleRt.anchorMax = Vector2.one;
			hHandleRt.sizeDelta = new Vector2(-20, -20);
			hHandleGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
			hBar.handleRect = hHandleRt;

			// 4. 创建垂直滚动条
			GameObject vScrollbarGo = new GameObject("Scrollbar Vertical",
				typeof(RectTransform), typeof(Scrollbar), typeof(Image));
			RectTransform vBarRt = vScrollbarGo.GetComponent<RectTransform>();
			vBarRt.anchorMin = new Vector2(1, 0);
			vBarRt.anchorMax = new Vector2(1, 1);
			vBarRt.pivot = new Vector2(1, 0.5f);
			vBarRt.anchoredPosition = new Vector2(-4, 0);
			vBarRt.sizeDelta = new Vector2(20, -16);
			Image vBarImg = vScrollbarGo.GetComponent<Image>();
			vBarImg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
			Scrollbar vBar = vScrollbarGo.GetComponent<Scrollbar>();
			vBar.direction = Scrollbar.Direction.BottomToTop;

			GameObject vSlidingGo = new GameObject("Sliding Area",
				typeof(RectTransform));
			vSlidingGo.transform.SetParent(vScrollbarGo.transform, false);
			RectTransform vSlidingRt = vSlidingGo.GetComponent<RectTransform>();
			vSlidingRt.anchorMin = Vector2.zero;
			vSlidingRt.anchorMax = Vector2.one;
			vSlidingRt.sizeDelta = new Vector2(-20, -20);

			GameObject vHandleGo = new GameObject("Handle",
				typeof(RectTransform), typeof(Image));
			vHandleGo.transform.SetParent(vSlidingGo.transform, false);
			RectTransform vHandleRt = vHandleGo.GetComponent<RectTransform>();
			vHandleRt.anchorMin = Vector2.zero;
			vHandleRt.anchorMax = Vector2.one;
			vHandleRt.sizeDelta = new Vector2(-20, -20);
			vHandleGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
			vBar.handleRect = vHandleRt;

			// 5. 创建 ScrollView 根节点
			GameObject scrollGo = new GameObject("Xeric UI Action Table",
				typeof(RectTransform), typeof(ScrollRect), typeof(Image));
			RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
			scrollRt.sizeDelta = new Vector2(800, 600);
			scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.3f);

			ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
			scrollRect.content = contentRt;
			scrollRect.viewport = vpRt;
			scrollRect.horizontalScrollbar = hBar;
			scrollRect.verticalScrollbar = vBar;
			scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;

			// 组装层级
			vpRt.SetParent(scrollRt, false);
			hScrollbarGo.transform.SetParent(scrollRt, false);
			vScrollbarGo.transform.SetParent(scrollRt, false);

			// 绑定 ScrollRect 到表格（通过 SerializedObject 正确回写）
			using (SerializedObject so = new SerializedObject(table))
			{
				SerializedProperty prop = so.FindProperty("m_ScrollRect");
				if (prop != null)
				{
					prop.objectReferenceValue = scrollRect;
					so.ApplyModifiedProperties();
				}
			}

			// 挂载到 Canvas
			GameObject parent = GetOrCreateCanvas();
			scrollRt.SetParent(parent.transform, false);

			Undo.RegisterCreatedObjectUndo(scrollGo, "Create Xeric UI Action Table");
			Selection.activeGameObject = scrollGo;
		}

		private static GameObject GetOrCreateCanvas()
		{
			Canvas canvas = Object.FindObjectOfType<Canvas>();
			if (canvas != null && canvas.isRootCanvas)
				return canvas.gameObject;

			GameObject canvasGo = new GameObject("Canvas",
				typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			return canvasGo;
		}

		#endregion
	}
}
