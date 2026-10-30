using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XericUI.ReflectionUIGenerator;
using XericUI.Core;

namespace XericUIEditor.ReflectionUIGenerator
{
    /// <summary>
    /// 反射界面生成器的 Hierarchy 右键菜单示例
    /// 在 Hierarchy 中右键 -&gt; Xeric UI -&gt; Create Reflection UI Example 即可快速创建示例场景
    /// 
    /// 创建后：
    /// 1. 将配置好的 XuiavrgPropertyGroupScriptableObject 通过代码传入 Manager
    /// 2. 调用 manager.BuildEntries(new XuiavrgExampleTestData()) 构建 Entry
    /// 3. 调用 manager.GenerateUI() 生成界面
    /// </summary>
    public static class XuiavrgExampleMenu
    {
        private const string MenuPath = XericRegistrationPaths.UICreate + "/Create Reflection UI Example";
        private const int MenuPriority = 10;

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void CreateReflectionUIExample()
        {
            // 1. 确保有 Canvas（如果没有则创建）
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Reflection UI Example");
            }

            // 2. 确保有 EventSystem
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create Reflection UI Example");
            }

            // 3. 创建示例根节点（带 Manager 和 Grid 布局）
            GameObject rootPanel = new GameObject("ReflectionUIExample",
                typeof(RectTransform), typeof(XuiavrgInspectorManager));
            Undo.RegisterCreatedObjectUndo(rootPanel, "Create Reflection UI Example");
            rootPanel.transform.SetParent(canvas.transform, false);

            RectTransform rootRt = rootPanel.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = new Vector2(100, 100);
            rootRt.offsetMax = new Vector2(-100, -100);

            // 添加背景 Image
            Image rootImage = rootPanel.AddComponent<Image>();
            rootImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // 4. 创建 Grid 布局的内容区域（作为 Entry 生成的父节点）
            GameObject contentArea = new GameObject("ContentGrid",
                typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(contentArea, "Create Reflection UI Example");
            contentArea.transform.SetParent(rootPanel.transform, false);

            RectTransform contentRt = contentArea.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(20, 20);
            contentRt.offsetMax = new Vector2(-20, -20);

            // 配置 GridLayoutGroup
            GridLayoutGroup gridLayout = contentArea.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(400, 60);
            gridLayout.spacing = new Vector2(10, 10);
            gridLayout.padding = new RectOffset(10, 10, 10, 10);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 1;

            // 配置 ContentSizeFitter - 垂直方向自适应
            ContentSizeFitter fitter = contentArea.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 5. 选中新创建的节点
            Selection.activeGameObject = rootPanel;

            Debug.Log("[Xuiavrg] 反射界面示例已创建。\n" +
                       "使用步骤：\n" +
                       "1. 创建 XuiavrgPropertyGroupScriptableObject 并设置 groupName/supportedTags\n" +
                       "2. 调用 manager.BuildEntries(new XuiavrgExampleTestData())\n" +
                       "3. 调用 manager.GenerateUI()");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCreateReflectionUIExample()
        {
            return true;
        }
    }
}
