using System;
using Deconstruction.Runtime;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XericUI.Form;

namespace XericUI.Core.Base
{
    public class XericUIBehaviour : UIBehaviour, IActivity
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

        /// <summary>
        /// 将窗口定位到目标 RectTransform 的指定锚点位置
        /// </summary>
        /// <param name="target">目标 RectTransform</param>
        /// <param name="anchor">锚点偏移，(0,0)=左下, (1,1)=右上</param>
        public void SetAnchorPosition(RectTransform target, Vector2 anchor)
        {
            var rt = rectTransform;
            if (rt == null || target == null) return;

            if (rt.parent == target.parent)
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = anchor;
                rt.anchoredPosition = Vector2.zero;
                return;
            }

            var targetWorldPos = target.TransformPoint(new Vector3(anchor.x, anchor.y, 0));

            // RectTransformUtility 会自动处理父级不一致的情况
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt.parent as RectTransform,
                    targetWorldPos,
                    null,
                    out Vector2 anchorLocalPos))
            {
                Vector2 pivotOffset = new Vector2(
                    (rt.pivot.x - 0.5f) * rt.rect.width,
                    (rt.pivot.y - 0.5f) * rt.rect.height
                );
                rt.anchoredPosition = anchorLocalPos + pivotOffset;
            }
        }
        
        /// <summary>
        /// 根据目标单元格与父级画布的可用空间定位弹窗。
        /// 默认以左上角对齐单元格左下角；下方空间不足时改用底部锚点显示在单元格上方，
        /// 右侧空间不足时改为从右侧向左展开。
        /// </summary>
        public void AutoSnapAlignedToRect(RectTransform cellRect)
        {
            var rt = transform as RectTransform;
            if (rt == null || cellRect == null) return;

            var parentRT = rt.parent as RectTransform;
            if (parentRT == null) return;

            Canvas.ForceUpdateCanvases();

            var cellCorners = new Vector3[4];
            cellRect.GetWorldCorners(cellCorners);
            for (int i = 0; i < cellCorners.Length; i++)
                cellCorners[i] = parentRT.InverseTransformPoint(cellCorners[i]);

            var popupWidth = parentRT.InverseTransformVector(
                rt.TransformVector(Vector3.right * rt.rect.width)).magnitude;
            var popupHeight = parentRT.InverseTransformVector(
                rt.TransformVector(Vector3.up * rt.rect.height)).magnitude;
            var parentRect = parentRT.rect;

            var leftSpace = cellCorners[2].x - parentRect.xMin;
            var rightSpace = parentRect.xMax - cellCorners[0].x;
            var placeRight = rightSpace >= popupWidth || rightSpace >= leftSpace;

            var bottomSpace = cellCorners[0].y - parentRect.yMin;
            var topSpace = parentRect.yMax - cellCorners[1].y;
            var placeBelow = bottomSpace >= popupHeight || bottomSpace >= topSpace;

            rt.pivot = new Vector2(placeRight ? 0f : 1f, placeBelow ? 1f : 0f);
            var target = new Vector3(
                placeRight ? cellCorners[0].x : cellCorners[2].x,
                placeBelow ? cellCorners[0].y : cellCorners[1].y,
                0f);
            rt.position = parentRT.TransformPoint(target);
        }
        
        #endregion

        #region 显影

        public virtual void Show() => gameObject.SetActive(true);

        public virtual void Hide() => gameObject.SetActive(false);

        #endregion
    }
}