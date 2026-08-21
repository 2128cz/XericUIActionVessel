using System;
using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表工具抽象基类。
    ///
    /// 每个工具对应一个独立的视觉元素（背景网格、折线、散点等），
    /// 由 ChartToolsManager 统一编排生命周期。
    ///
    /// 生命周期顺序：
    ///   InternalInitialize (构造阶段)
    ///   → OnInitialize (派生初始化)
    ///   → OnUpdate (每帧更新)
    ///   → OnRender (每帧渲染)
    ///   → OnDispose (销毁)
    /// </summary>
    public abstract class XChartToolBase : IDisposable
    {
        // ── 编辑器/运行时统一 hideFlags ────────────────────────────

        private const HideFlags k_EditorFlags = HideFlags.DontSaveInEditor;
        private const HideFlags k_PlayFlags = HideFlags.HideAndDontSave;

        protected static HideFlags CurrentFlags =>
            Application.isPlaying ? k_PlayFlags : k_EditorFlags;

        // ── 基础属性 ──────────────────────────────────────────────

        protected XChartToolsManager Manager { get; private set; }
        public int OrderIndex { get; set; }
        public GameObject RootObject { get; private set; }
        public RectTransform RootTransform { get; private set; }
        public string ToolName { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsInitialized { get; protected set; }

        // ── 构造 ──────────────────────────────────────────────────

        protected XChartToolBase() { }

        internal void InternalInitialize(XChartToolsManager manager, string name,
            GameObject rootObject, int orderIndex)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            ToolName = name;
            RootObject = rootObject ?? throw new ArgumentNullException(nameof(rootObject));
            RootTransform = rootObject.GetComponent<RectTransform>();
            if (RootTransform == null)
                RootTransform = rootObject.AddComponent<RectTransform>();
            OrderIndex = orderIndex;

            rootObject.hideFlags = CurrentFlags;

            OnInitialize();
            IsInitialized = true;
        }

        // ── 生命周期 ──────────────────────────────────────────────

        protected virtual void OnInitialize() { }
        public virtual void OnUpdate() { }
        public virtual void OnRender() { }
        protected virtual void OnDispose() { }

        public void Dispose()
        {
            OnDispose();
            if (RootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(RootObject);
                RootObject = null;
            }
            IsInitialized = false;
        }

        // ── 子组件管理 ────────────────────────────────────────────

        protected RectTransform CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(RootTransform, false);
            child.hideFlags = CurrentFlags;

            var rt = child.GetComponent<RectTransform>();
            if (rt == null) rt = child.AddComponent<RectTransform>();
            return rt;
        }

        protected T CreateChildComponent<T>(string childName) where T : Component
        {
            var child = CreateChild(childName);
            var comp = child.gameObject.GetComponent<T>();
            if (comp == null) comp = child.gameObject.AddComponent<T>();
            return comp;
        }

        public virtual void SetVisible(bool visible)
        {
            if (RootObject != null) RootObject.SetActive(visible);
        }
    }
}