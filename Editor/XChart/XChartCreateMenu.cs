using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XericUI.XChart.Line;
using XericUI.XChart.Pseudo3D;
using XericUI.XChart.Radar;

namespace XericUI.XChart.Editor
{
    internal static class XChartCreateMenu
    {
        [MenuItem("GameObject/Xeric/XChart/Line Chart", false, 10)]
        private static void CreateLineChart() => Create<XLineChart>("XLineChart");

        [MenuItem("GameObject/Xeric/XChart/Radar Chart", false, 11)]
        private static void CreateRadarChart() => Create<XRadarChart>("XRadarChart");

        [MenuItem("GameObject/Xeric/XChart/Pseudo3D Scatter Chart", false, 12)]
        private static void CreateScatterChart() => Create<XPseudo3DScatterChart>("XPseudo3DScatterChart");

        private static void Create<T>(string name) where T : Component
        {
            Canvas canvas = Selection.activeTransform == null ? null : Selection.activeTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            }
            var chart = new GameObject(name, typeof(RectTransform), typeof(T));
            var rect = chart.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 360f);
            Undo.RegisterCreatedObjectUndo(chart, "Create XChart");
            Selection.activeGameObject = chart;
        }
    }
}
