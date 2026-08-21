using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表主行为组件基类。
    ///
    /// 标记 [ExecuteAlways] 确保编辑器下生命周期正常执行。
    /// 派生类在 Start() 中创建工具并配置数据模型。
    /// </summary>
    [AddComponentMenu("Xeric/XChart/Chart Base")]
    [ExecuteAlways]
    public abstract class XChartBase : MonoBehaviour
    {
        [Header("工具管理")]
        [SerializeField] private bool m_ShowDebug;
        [SerializeField] private bool m_HideAllTools;

        public XChartToolsManager ToolManager { get; private set; }
        public Transform ToolsRoot { get; private set; }

        private bool m_IsInitialized;

        // ── hideFlags ─────────────────────────────────────────────

        private const HideFlags k_EditorFlags = HideFlags.DontSaveInEditor;
        private const HideFlags k_PlayFlags = HideFlags.HideAndDontSave;

        internal static HideFlags CurrentFlags =>
            Application.isPlaying ? k_PlayFlags : k_EditorFlags;

        // ── 生命周期 ──────────────────────────────────────────────

        private void Awake()
        {
            Initialize();
        }

        protected virtual void OnEnable()
        {
            if (!m_IsInitialized) Initialize();
        }

        private void Initialize()
        {
            if (m_IsInitialized) return;
            if (ToolManager != null) return;

            var flags = CurrentFlags;

            var toolsObj = new GameObject("[XChartTools]");
            toolsObj.transform.SetParent(transform, false);
            toolsObj.hideFlags = flags;
            ToolsRoot = toolsObj.transform;

            ToolManager = new XChartToolsManager
            {
                ToolsRoot = ToolsRoot,
            };

            m_IsInitialized = true;
        }

        protected virtual void Update()
        {
            if (!m_IsInitialized || ToolManager == null) return;
            ToolManager.UpdateAll();
        }

        protected virtual void LateUpdate()
        {
            if (!m_IsInitialized || ToolManager == null) return;
            ToolManager.RenderAll();
        }

        // ── 工具管理 ──────────────────────────────────────────────

        public T AddTool<T>(string toolName, int orderIndex = 0)
            where T : XChartToolBase, new()
        {
            if (ToolManager == null) Initialize();
            return ToolManager.AddTool<T>(toolName, orderIndex);
        }

        public bool RemoveTool(string toolName)
        {
            if (ToolManager == null) return false;
            return ToolManager.RemoveToolByName(toolName);
        }

        public void RemoveAllTools() => ToolManager?.RemoveAllTools();

        // ── 清理 ──────────────────────────────────────────────────

        protected virtual void OnDestroy()
        {
            if (ToolManager != null)
            {
                ToolManager.Dispose();
                ToolManager = null;
            }
            if (ToolsRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(ToolsRoot.gameObject);
                else
                    DestroyImmediate(ToolsRoot.gameObject);
                ToolsRoot = null;
            }
            m_IsInitialized = false;
        }

        private void OnValidate()
        {
            // 由派生类按需扩展
        }
    }
}