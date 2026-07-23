using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.XTable.Core;
using XericUI.XTable.Rendering.Component;

namespace XericUIEditor.XTable
{
    /// <summary>
    /// XericUIActionTable 的 Inspector 编辑器——
    /// 折叠分组：[样式设定] [尺寸设定] [调试] [数据浏览]。
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

        internal const string MENU_PATH = "GameObject/Xeric Library/Table/Xeric UI Action Table";

        // 折叠状态
        private static bool s_StyleFoldout = true;
        private static bool s_SizeFoldout = true;
        private static bool s_DebugFoldout;
        private static bool s_DataBrowseFoldout;

        // 调试参数
        private bool m_ShowChildren;

        private void OnEnable()
        {
            m_Table = (XericUIActionTable)target;

            m_RowHeightsProp = serializedObject.FindProperty("m_RowHeights");
            m_ColWidthsProp = serializedObject.FindProperty("m_ColWidths");
            m_DefaultStyleNsProp = serializedObject.FindProperty("m_DefaultStyleNamespace");
            m_ScrollRectProp = serializedObject.FindProperty("m_ScrollRect");

            // 同步表格的 ChildrenVisible 状态到编辑器 toggle
            m_ShowChildren = m_Table.ChildrenVisible;
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

                // 冻结行列
                var freezeRowProp = serializedObject.FindProperty("m_FreezeRowCount");
                var freezeColProp = serializedObject.FindProperty("m_FreezeColCount");
                if (freezeRowProp != null)
                    EditorGUILayout.PropertyField(freezeRowProp, new GUIContent("冻结行数"));
                if (freezeColProp != null)
                    EditorGUILayout.PropertyField(freezeColProp, new GUIContent("冻结列数"));

                EditorGUI.indentLevel--;
            }

            // ===== 调试 =====
            EditorGUILayout.Space(4);
            s_DebugFoldout = EditorGUILayout.Foldout(s_DebugFoldout, "调试", true);
            if (s_DebugFoldout)
            {
                EditorGUI.indentLevel++;

                if (GUILayout.Button("重新生成表格", GUILayout.Height(26)))
                {
                    m_Table.FullRebuildPreservingData();
                    m_Table.SetChildrenVisible(m_ShowChildren);
                    EditorApplication.RepaintHierarchyWindow();
                }

                if (GUILayout.Button("强制刷新样式", GUILayout.Height(26)))
                {
                    m_Table.ForceStyleRefresh();
                    m_Table.SetChildrenVisible(m_ShowChildren);
                    EditorApplication.RepaintHierarchyWindow();
                }

                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("导出到剪贴板", GUILayout.Height(26)))
                {
                    var data = m_Table.BuildClipboardData();
                    MacroFile.SetClipboardObject(data, MacroFile.JsonSerializerFormatter.formatterWithZip);
                    Debug.Log($"[XTable] 表格数据已导出到剪贴板 ({data.Cells?.Count ?? 0} 个单元格, {data.Merges?.Count ?? 0} 个合并)");
                }

                if (GUILayout.Button("从剪贴板导入", GUILayout.Height(26)))
                {
                    var data = MacroFile.GetClipboardObject<XTableClipboardData>(MacroFile.JsonSerializerFormatter
                        .formatterWithZip);
                    if (data != null)
                    {
                        Undo.RecordObject(m_Table, "Import Table from Clipboard");
                        m_Table.ApplyClipboardData(data);
                        serializedObject.Update();
                        Repaint();
                        Debug.Log(
                            $"[XTable] 已从剪贴板导入表格数据 ({data.Cells?.Count ?? 0} 个单元格, {data.Merges?.Count ?? 0} 个合并)");
                    }
                    else
                    {
                        Debug.LogWarning("[XTable] 剪贴板中没有有效的表格数据");
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                bool newShow = EditorGUILayout.Toggle("显示子项", m_ShowChildren);
                if (newShow != m_ShowChildren)
                {
                    m_ShowChildren = newShow;
                    m_Table.SetChildrenVisible(m_ShowChildren);
                    EditorApplication.RepaintHierarchyWindow();
                }

                if (m_ShowChildren)
                {
                    EditorGUILayout.HelpBox(
                        "子项已显示：所有动态生成的对象将在 Hierarchy 中可见（仍不保存到场景）。\n" +
                        "用于排查表格元素堆积或渲染异常问题。",
                        MessageType.Info);
                }

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

                // ── 单元格配置 ──
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("单元格配置", EditorStyles.boldLabel);
                int selRow = m_Table.SelectedRow;
                int selCol = m_Table.SelectedCol;
                if (selRow < 0 || selCol < 0)
                {
                    EditorGUILayout.HelpBox("请先在 Scene View 中点击选中一个单元格", MessageType.Info);
                }
                else
                {
                    var data = m_Table.TableData?.GetCell(selRow, selCol);
                    if (data == null)
                    {
                        EditorGUILayout.HelpBox("单元格数据为空", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.TextField("目标单元格", $"({selRow}, {selCol})");
                        EditorGUI.EndDisabledGroup();

                        EditorGUI.BeginChangeCheck();

                        // 内容类型
                        CellContentType ct = (CellContentType)EditorGUILayout.EnumPopup("内容类型", data.ContentType);

                        // 根据类型显示对应字段
                        switch (ct)
                        {
                            case CellContentType.Text:
                                EditorGUILayout.LabelField("文本内容", data.Text ?? "(空)");
                                break;
                            case CellContentType.Image:
                                EditorGUILayout.ObjectField("图片纹理", data.Image, typeof(Texture2D), false);
                                break;
                            case CellContentType.Prefab:
                                GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                                    "预制体", data.Prefab, typeof(GameObject), false);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(m_Table, "Set Cell Prefab");
                                    data.Prefab = newPrefab;
                                    data.ContentType = ct;
                                    m_Table.SetCellPrefab(selRow, selCol, newPrefab);
                                    EditorUtility.SetDirty(m_Table);
                                    Repaint();
                                }

                                goto skipEndChangeCheck;
                        }

                        // 默认 Text 类型
                        string newText = EditorGUILayout.TextField("文本内容", data.Text ?? "");
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(m_Table, "Edit Cell");
                            data.Text = newText;
                            data.ContentType = ct;
                            EditorUtility.SetDirty(m_Table);
                            m_Table.MarkDirty(TableDirtyType.DataChanged);
                            EditorUtility.SetDirty(m_Table);
                            Repaint();
                        }

                        skipEndChangeCheck: ;

                        // 样式
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("单元格样式 (StyleMember)", EditorStyles.miniBoldLabel);
                        EditorGUI.BeginChangeCheck();
                        string ns = EditorGUILayout.TextField("命名空间",
                            data.CellStyle?.CurrentNamespace ?? "");
                        string path = EditorGUILayout.TextField("样式路径",
                            data.CellStyle?.StylePath ?? "");
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(m_Table, "Edit Cell Style");
                            if (data.CellStyle == null)
                                data.CellStyle = new StyleMember();
                            data.CellStyle.CurrentNamespace = ns;
                            data.CellStyle.StylePath = path;
                            EditorUtility.SetDirty(m_Table);
                            m_Table.MarkDirty(TableDirtyType.DataChanged);
                            Repaint();
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Hierarchy 创建菜单

        [MenuItem(MENU_PATH, false, 60)]
        private static void CreateTable(MenuCommand menuCommand)
        {
            // 1. 创建根节点：ScrollRect + Image + XericUIActionTable 共存于同一 GameObject
            GameObject rootGo = new GameObject("Xeric UI Action Table",
                typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(XericUIActionTable));
            RectTransform rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(800, 600);
            rootGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.3f);

            ScrollRect scrollRect = rootGo.GetComponent<ScrollRect>();
            XericUIActionTable table = rootGo.GetComponent<XericUIActionTable>();

            // 2. 创建 Content（纯 RectTransform，由表格组件管理动态子对象）
            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(800, 600);

            // 3. 创建 Viewport（Mask 裁剪区域）
            GameObject viewportGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            RectTransform vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;
            contentRt.SetParent(vpRt, false);

            // 4. 创建水平滚动条
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
            hSlidingRt.sizeDelta = new Vector2(-20, 0);

            GameObject hHandleGo = new GameObject("Handle",
                typeof(RectTransform), typeof(Image));
            hHandleGo.transform.SetParent(hSlidingGo.transform, false);
            RectTransform hHandleRt = hHandleGo.GetComponent<RectTransform>();
            hHandleRt.anchorMin = Vector2.zero;
            hHandleRt.anchorMax = Vector2.one;
            hHandleRt.sizeDelta = new Vector2(-10, -10);
            hHandleGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            hBar.handleRect = hHandleRt;

            // 5. 创建垂直滚动条
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
            vSlidingRt.sizeDelta = new Vector2(0, -20);

            GameObject vHandleGo = new GameObject("Handle",
                typeof(RectTransform), typeof(Image));
            vHandleGo.transform.SetParent(vSlidingGo.transform, false);
            RectTransform vHandleRt = vHandleGo.GetComponent<RectTransform>();
            vHandleRt.anchorMin = Vector2.zero;
            vHandleRt.anchorMax = Vector2.one;
            vHandleRt.sizeDelta = new Vector2(-10, -10);
            vHandleGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            vBar.handleRect = vHandleRt;

            // 6. 配置 ScrollRect
            scrollRect.content = contentRt;
            scrollRect.viewport = vpRt;
            scrollRect.horizontalScrollbar = hBar;
            scrollRect.verticalScrollbar = vBar;
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 组装层级
            vpRt.SetParent(rootRt, false);
            hScrollbarGo.transform.SetParent(rootRt, false);
            vScrollbarGo.transform.SetParent(rootRt, false);

            // 绑定 ScrollRect 到表格
            using (SerializedObject so = new SerializedObject(table))
            {
                SerializedProperty prop = so.FindProperty("m_ScrollRect");
                if (prop != null)
                {
                    prop.objectReferenceValue = scrollRect;
                    so.ApplyModifiedProperties();
                }
            }

            // 确定父级：优先放在右键点击的对象下，其次放在其所在的 Canvas 下
            GameObject parent = GetCreateParent(menuCommand);
            rootRt.SetParent(parent.transform, false);

            Undo.RegisterCreatedObjectUndo(rootGo, "Create Xeric UI Action Table");
            Selection.activeGameObject = rootGo;
        }

        /// <summary>
        /// 确定创建表格时的父级对象。
        /// 优先使用右键菜单点击的 GameObject（要求其处于 Canvas 层级下）；
        /// 若没有任何 Canvas 则自动创建一个。
        /// </summary>
        private static GameObject GetCreateParent(MenuCommand menuCommand)
        {
            // 右键菜单点击的对象
            GameObject clickedGo = menuCommand?.context as GameObject;
            if (clickedGo == null && menuCommand?.context is Component comp)
                clickedGo = comp.gameObject;

            // 点击的对象在 Canvas 层级下 → 直接作为父级
            if (clickedGo != null && clickedGo.GetComponentInParent<Canvas>() != null)
                return clickedGo;

            // 兜底：场景中的 Canvas 或自动创建
            return GetOrCreateCanvas();
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