using System;
using Deconstruction.Runtime;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XericUI.Core.Base
{
    public class XericUIBehaviour : UIBehaviour
    {
#if ODIN_INSPECTOR
        [InfoBox("脚本必填属性存在空引用！请检查开放属性后再试", InfoMessageType.Error, VisibleIf = "PublicFieldNullReference")]
        [HideLabel, DisplayAsString]
#endif
        string __EMPTYFIELD;


        protected bool PublicFieldNullReference;

        [System.NonSerialized] private Canvas _canvas;
        [System.NonSerialized] private CanvasScaler _canvasScaler;
        [System.NonSerialized] private GraphicRaycaster _graphicRaycaster;
        [System.NonSerialized] private RectTransform _rect;

        /// <summary>
        /// 层级脏（层级被转移）
        /// </summary>
        private bool _hierarchyDirty;

        /// <summary>
        /// 根布局管理器
        /// </summary>
        public Canvas canvas
        {
            get
            {
                if (!_canvas) _canvas = GetComponentInParent<Canvas>();
#if UNITY_EDITOR
                if (!_canvas) Debug.LogError($"{name} 无法找到布局组件，接下来的流程可能会引发错误", this);
#endif
                return _canvas;
            }
        }

        /// <summary>
        /// 根布局缩放
        /// </summary>
        public CanvasScaler canvasScaler
        {
            get
            {
                if (!_canvasScaler) _canvasScaler = GetComponentInParent<CanvasScaler>();
#if UNITY_EDITOR
                if (_canvas == null) Debug.LogError($"{name} 无法找到布局缩放组件，接下来的流程可能会引发错误", this);
#endif
                return _canvasScaler;
            }
        }

        /// <summary>
        /// 根布局射线检测
        /// </summary>
        public GraphicRaycaster graphicRaycaster
        {
            get
            {
                if (!_graphicRaycaster) _graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
#if UNITY_EDITOR
                if (_canvas == null) Debug.LogError($"{name} 无法找到布局射线检测，接下来的流程可能会引发错误", this);
#endif
                return _graphicRaycaster;
            }
        }

        /// <summary>
        /// ui变换组件
        /// </summary>
        protected RectTransform rectTransform
        {
            get
            {
                if (!_rect) _rect = GetComponent<RectTransform>();
                return _rect;
            }
        }

        #region 生命周期

        protected bool AlreadyAwake { get; private set; }
        protected bool AlreadyStart { get; private set; }
        protected bool AlreadyOnEnable { get; private set; }
        protected bool AlreadyOnDisable { get; private set; }
        protected bool FirstUpdate { get; private set; }
        protected bool FirstFixUpdate { get; private set; }

        protected override void Awake()
        {
            AlreadyAwake = true;
            base.Awake();
        }

        protected override void Start()
        {
            AlreadyStart = true;
            base.Start();
        }

        protected override void OnEnable()
        {
            AlreadyOnEnable = true;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            AlreadyOnDisable = true;
            base.OnDisable();
        }

        #endregion

        #region 脏处理

        /// <summary>
        /// 设置层级脏()
        /// </summary>
        public void SetHierarchyDirty()
        {
            rectTransform.SetLayoutDirty();
            _canvas = null;
            _canvasScaler = null;
            _graphicRaycaster = null;
            _hierarchyDirty = true;
        }
        
        /// <summary>
        /// 复位层级脏
        /// </summary>
        public void ResetHierarchyDirty()
        {
            _hierarchyDirty = false;    
            rectTransform.SetLayoutDirty();
        }

        #endregion

        #region 锚点设置

        /// <summary>
        /// 设置锚点并保持视觉位置和尺寸不变（补偿偏移量抵消锚点变化带来的位移）
        /// </summary>
        protected void SetAnchor(Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = rectTransform;
            if (rt == null) return;

            var parentRt = rt.parent != null ? rt.parent as RectTransform : null;
            if (parentRt == null)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                return;
            }

            Vector2 parentSize = parentRt.rect.size;

            // 计算当前在父空间中的实际边界
            float left = parentSize.x * rt.anchorMin.x + rt.offsetMin.x;
            float right = parentSize.x * rt.anchorMax.x + rt.offsetMax.x;
            float bottom = parentSize.y * rt.anchorMin.y + rt.offsetMin.y;
            float top = parentSize.y * rt.anchorMax.y + rt.offsetMax.y;

            // 设置新锚点
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;

            // 补偿偏移量以保持视觉位置和尺寸不变
            rt.offsetMin = new Vector2(
                left - parentSize.x * anchorMin.x,
                bottom - parentSize.y * anchorMin.y
            );
            rt.offsetMax = new Vector2(
                right - parentSize.x * anchorMax.x,
                top - parentSize.y * anchorMax.y
            );
        }

        /// <summary>
        /// 设置锚点，通过参数控制是否保持位置
        /// </summary>
        /// <param name="preservePosition">true=保持位置和尺寸不变（同两参数版本）；false=位置被动改变，仅保持尺寸</param>
        protected void SetAnchor(Vector2 anchorMin, Vector2 anchorMax, bool preservePosition)
        {
            if (preservePosition)
            {
                SetAnchor(anchorMin, anchorMax);
            }
            else
            {
                var rt = rectTransform;
                if (rt == null) return;
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
            }
        }

        /// <summary>
        /// 设置锚点和anchoredPosition，保持尺寸不变
        /// </summary>
        protected void SetAnchor(Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
        {
            var rt = rectTransform;
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
        }

        /// <summary>
        /// 设置锚点、anchoredPosition和sizeDelta
        /// </summary>
        protected void SetAnchor(Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rt = rectTransform;
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        #endregion

        #region 显影

        public virtual void Show()
        {
            gameObject.SetActive(true);            
        }
        
        public void Show(bool isShow)
        {
            if (isShow)
                Show();
            else
                Hide();
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}