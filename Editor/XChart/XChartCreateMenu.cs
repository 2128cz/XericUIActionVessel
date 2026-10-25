using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;
using XericUI.XChart.Line;
using XericUI.XChart.Products;
using XericUI.XChart.Pseudo3D;
using XericUI.XChart.Radar;

namespace XericUI.XChart.Editor
{
    internal static class XChartCreateMenu
    {
        [MenuItem(XericRegistrationPaths.ChartCreate + "/Line Chart", false, 10)]
        private static void CreateLineChart() => Create<XLineChart>("XLineChart");

        [MenuItem(XericRegistrationPaths.ChartCreate + "/Radar Chart", false, 11)]
        private static void CreateRadarChart() => Create<XRadarChart>("XRadarChart");

        [MenuItem(XericRegistrationPaths.ChartCreate + "/Pseudo3D Scatter Chart", false, 12)]
        private static void CreateScatterChart() => Create<XPseudo3DScatterChart>("XPseudo3DScatterChart");

        [MenuItem(XericRegistrationPaths.ChartCreate + "/网格渲染预览图", false, 13)]
        private static void CreateMeshPreview() => Create<XMeshPreviewChart>("XMeshPreviewChart");

        /// <summary>
        /// 建一个图表对象。
        ///
        /// <para><b>不替用户决定宿主结构</b>：只在当前位置放不下时给出警告，绝不自动创建 Canvas——
        /// 自动建出来的 Canvas 会多出一个用户没要求的根物体，预制体里尤其碍事。
        /// 没有 Canvas 祖先时的现象是"什么都画不出来"，所以 <see cref="ValidateRenderingContext"/>
        /// 必须把这句话说出口。</para>
        ///
        /// <para><b>预制体编辑模式要特殊处理</b>：此时对象必须挂进预制体根，否则它建在了预制体外面，
        /// 退出预制体时会被直接丢掉（"组件完全不存在"就是这么来的）。</para>
        /// </summary>
        private static void Create<T>(string name) where T : Component
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            Transform parent = stage != null
                ? stage.prefabContentsRoot.transform
                : FindParentForScene();

            var chart = new GameObject(name, typeof(RectTransform), typeof(T));
            var rect = chart.GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 360f);

            Undo.RegisterCreatedObjectUndo(chart, "Create XChart");
            Selection.activeGameObject = chart;

            if (stage != null)
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
            }

            ValidateRenderingContext(chart);
        }

        /// <summary>选中项所在 Canvas 优先，其次直接挂在选中物体下；都没选中就建在场景根</summary>
        private static Transform FindParentForScene()
        {
            Transform selection = Selection.activeTransform;
            if (selection == null) return null;

            Canvas canvas = selection.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.transform;

            return selection is RectTransform ? selection : null;
        }

        /// <summary>没有 Canvas 祖先时给出明确提示——否则现象是"什么都看不见"，很难查</summary>
        private static void ValidateRenderingContext(GameObject chart)
        {
            if (chart.GetComponentInParent<Canvas>() != null) return;
            Debug.LogWarning(
                "XChart：这个图表没有 Canvas 祖先，UGUI 图元不会渲染（现象就是什么都看不到）。" +
                "把它放到某个 Canvas 下，或在它上面挂一个 Canvas 组件。", chart);
        }
    }
}