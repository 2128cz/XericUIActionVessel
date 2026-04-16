using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Helper;

#if UNITY_EDITOR
using UnityEditor;
using XericLibraryEditor.Debug;
#endif
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace XericUI.OverrideLayout
{
    [AddComponentMenu("Xeric UI Vessel/ScrollRect", 50)]
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class XericScrollRect : ScrollRect
    {
        #region 事件委托

        // public Action 

        #endregion

        #region 字段属性

        /// <summary>
        /// 启用自动剔除功能
        /// </summary>
        public bool enableAutoActiveByMaskVisible = true;

        /// <summary>
        /// 容器布局
        /// </summary>
        public LayoutGroup layoutGroup;

        /// <summary>
        /// 容器预加载像素范围
        /// </summary>
        public float preloadMargin = 200f;

        /// <summary>
        /// 容器加载响应可被察觉的变化长度
        /// </summary>
        public float discernibleDistance = 10f;

        /// <summary>
        /// 自动剔除更新速率
        /// </summary>
        [Tooltip("当设定满速率时，实际上是期望每帧执行更新")]
        // ReSharper disable once InconsistentNaming
        [Range(0.02f, 1)]
        public float AAMVUpdateRate = .9f;

        #endregion

        #region 布局参考

        private float _aamvUpdateTime;

        private Vector2 _lastContextSize;
        private Vector2 _lastContextAnchoredPosition;

        // 脏标记处理率，每次激活脏标记时给予多份权重，每次空处理则返还一部分权重,如果出现标记大于0的情况，说明脏标记处理次数太多
        private float _dirtyTakeRate;

        private HashSet<Transform> _lastActiveSet;
        
        #endregion

        #region 生命周期

#if UNITY_EDITOR
        private Vector3[] vertex1 = new Vector3[4];
        private Vector3[] vertex2 = new Vector3[4];
        private Vector3[] vertex3 = new Vector3[4];
        private void OnDrawGizmosSelected()
        {
            var lossyScale = transform.lossyScale;
            var scale = lossyScale * preloadMargin;
            var dis2 = lossyScale * discernibleDistance;
            
            content.GetWorldCorners(vertex1);
            viewport.GetWorldCorners(vertex2);
            var rect = new Rect(vertex2[0], new Vector2(vertex2[2].x - vertex2[0].x, vertex2[2].y - vertex2[0].y));
            var viewportMin = rect.min * lossyScale;
            var viewportMax = rect.max * lossyScale;
            // vertex3[0] = new Vector3(vertex2[0].x - scale.x, vertex2[0].y - scale.y, vertex2[0].z); 
            // vertex3[1] = new Vector3(vertex2[1].x - scale.x, vertex2[1].y + scale.y, vertex2[1].z); 
            // vertex3[2] = new Vector3(vertex2[2].x + scale.x, vertex2[2].y + scale.y, vertex2[2].z); 
            // vertex3[3] = new Vector3(vertex2[3].x + scale.x, vertex2[3].y - scale.y, vertex2[3].z); 
            
            vertex3[0] = new Vector3(rect.min.x - scale.x, rect.min.y - scale.y, vertex2[0].z); 
            vertex3[1] = new Vector3(rect.min.x - scale.x, rect.max.y + scale.y, vertex2[1].z); 
            vertex3[2] = new Vector3(rect.max.x + scale.x, rect.max.y + scale.y, vertex2[2].z); 
            vertex3[3] = new Vector3(rect.max.x + scale.x, rect.min.y - scale.y, vertex2[3].z); 
            
            Gizmos.color = Color.yellow;
            MacroGizmosDraw.DrawGizmos2RectLinkCube(vertex1, vertex3, Color.grey, Color.cyan, true);
            
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            
            // 布局
            if (layoutGroup == null)
                layoutGroup = transform.GetComponentInParent<LayoutGroup>();
            if (layoutGroup is XericHorizontalOrVerticalLayoutGroup xericLayoutGroup)
            {
                xericLayoutGroup.layoutIgnoreInactive = false;
            }
        }

        protected override void LateUpdate()
        {
            if (!enableAutoActiveByMaskVisible)
            {
                base.LateUpdate();
                return;
            }

            // 计算更新速率
            var targetFrameRate = Application.targetFrameRate;
            var frameAamvRate = Time.deltaTime / Mathf.Max(0.02f, AAMVUpdateRate);
            _dirtyTakeRate = Mathf.Max(_dirtyTakeRate - AAMVUpdateRate, 0);
            if (AAMVUpdateRate < 1)
            {
                if (_aamvUpdateTime < frameAamvRate)
                {
                    _aamvUpdateTime += Time.deltaTime;
                    base.LateUpdate();
                    return;
                }
                _aamvUpdateTime %= frameAamvRate;
            }

            var sqrDistance = MacroMath.SqrMagnitudeDistance(content.anchoredPosition, _lastContextAnchoredPosition);
            if (sqrDistance > discernibleDistance * discernibleDistance)
            {
                _lastContextAnchoredPosition = content.anchoredPosition;
                SetAAMVDirty();
            }

            base.LateUpdate();
        }

        private Vector3[] viewportVertex = new Vector3[4];
        /// <summary>
        /// 自动剔除更新标记
        /// </summary>
        protected void SetAAMVDirty()
        {
            _dirtyTakeRate += AAMVUpdateRate * 2;

            var nowActiveChildrenSet = HashSetPool<Transform>.Get();

            // 将视口尺寸转换到content本地空间
            // 手动计算视口在content空间的矩形
            viewport.GetWorldCorners(viewportVertex);
            var viewportRect = new Rect(
                content.InverseTransformPoint(viewportVertex[0]),
                content.InverseTransformPoint(viewportVertex[2]) - content.InverseTransformPoint(viewportVertex[0]));
            var contentRect = content.rect;
            
            // 计算预加载边距（考虑content缩放）
            var scaledMargin = preloadMargin / ((content.lossyScale.x + content.lossyScale.y) * 0.5f);
            
            // 扩展可见区域边界
            var visibleMin = viewportRect.min - new Vector2(preloadMargin, preloadMargin);
            var visibleMax = viewportRect.max + new Vector2(preloadMargin, preloadMargin);
            
            // 获取当前滚动归一化位置
            var scrollPos = new Vector2(
                horizontal ? horizontalScrollbar?.value ?? 0 : 0,
                vertical ? 1 - (verticalScrollbar?.value ?? 1) : 0);
            
            var isinit = _lastActiveSet == null;
            
            foreach (var child in content.GetChildren())
            {
                var rectTransform = child.RectTransform();
                // 直接使用anchoredPosition进行边界检测
                var childPos = rectTransform.anchoredPosition;
                var isVisible = 
                    childPos.x >= visibleMin.x && 
                    childPos.x <= visibleMax.x &&
                    childPos.y >= visibleMin.y && 
                    childPos.y <= visibleMax.y;
                var isActive = isinit || _lastActiveSet.Contains(child);

                if (isVisible)
                    nowActiveChildrenSet.Add(child);
                if (isVisible == isActive)
                    continue;
                
                child.gameObject.SetActive(isVisible);
            }

            if (!isinit)
                HashSetPool<Transform>.Release(_lastActiveSet);
            _lastActiveSet = nowActiveChildrenSet;
        }

        
        protected override void SetContentAnchoredPosition(Vector2 position)
        {
            base.SetContentAnchoredPosition(position);
            Debug.Log("123");
        }
        
        #endregion
        
        #region 组件替换
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/ScrollRect/Replace To XericScrollView", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is ScrollRect and not XericScrollRect;

        [UnityEditor.MenuItem("CONTEXT/ScrollRect/Replace To XericScrollView", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<ScrollRect, XericScrollRect,
                (RectTransform Viewport, RectTransform Content, Scrollbar HScrollbar, Scrollbar VScrollbar)>(
                o =>
                    (o.viewport, o.content, o.horizontalScrollbar, o.verticalScrollbar),
                (t, d) =>
                {
                    t.viewport = d.Viewport;
                    t.content = d.Content;
                    t.horizontalScrollbar = d.HScrollbar;
                    t.verticalScrollbar = d.VScrollbar;
                });

        [UnityEditor.MenuItem("CONTEXT/ScrollRect/Replace To ScrollView", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericScrollRect;

        [UnityEditor.MenuItem("CONTEXT/ScrollRect/Replace To ScrollView", false, 10)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericScrollRect, ScrollRect,
                (RectTransform Viewport, RectTransform Content, Scrollbar HScrollbar, Scrollbar VScrollbar)>(
                o =>
                    (o.viewport, o.content, o.horizontalScrollbar, o.verticalScrollbar),
                (t, d) =>
                {
                    t.viewport = d.Viewport;
                    t.content = d.Content;
                    t.horizontalScrollbar = d.HScrollbar;
                    t.verticalScrollbar = d.VScrollbar;
                });
#endif
        #endregion
    }
}