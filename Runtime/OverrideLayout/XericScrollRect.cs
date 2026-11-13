﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericLibrary.Runtime.Debuger;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core;
using XericUI.Core.Interface;
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
    [AddComponentMenu(XericRegistrationPaths.UI + "/ScrollRect", 50)]
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class XericScrollRect : ScrollRect
    {
        #region 字段属性

        [Tooltip("启用自动剔除功能")] [SerializeField] protected bool m_enableAdvancedFunc = true;

        [Tooltip("容器预加载像素范围")] [SerializeField]
        protected float m_preloadMargin = 200f;

        [Tooltip("容器加载响应可被察觉的变化长度(建议设置为子项的二维最小的尺寸)")] [SerializeField]
        protected float m_discernibleDistance = 10f;

        [Tooltip("自动剔除更新速率，当设定趋近零时，实际上是期望每帧执行更新")] [SerializeField]
        protected float m_AAMVUpdateRate = .1f;

        // grid布局组件
        [SerializeField] protected Vector2 m_cellSize = new Vector2(100, 100);
        [SerializeField] protected Vector2 m_spacing = Vector2.zero;
        [SerializeField] protected RectOffset m_padding;
        [SerializeField] protected GridLayoutGroup.Corner m_startCorner = GridLayoutGroup.Corner.UpperLeft;
        [SerializeField] protected GridLayoutGroup.Axis m_startAxis = GridLayoutGroup.Axis.Horizontal;
        [SerializeField] protected TextAnchor m_childAlignment = TextAnchor.UpperLeft;

        [SerializeField]
        protected GridLayoutGroup.Constraint m_constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        [SerializeField] protected int m_constraintCount = -1;

        /// <summary>
        /// 对象池
        /// </summary>
        [SerializeField] protected MacroPool.UnionSet childrenPool = new MacroPool.UnionSet();

        /// <summary>
        /// 对象数据
        /// </summary>
        [SerializeField] private readonly List<ScrollItemData> _childrenDatas = new List<ScrollItemData>();

        public Vector2 ContextSize
        {
            get => new Vector2(content.rect.width, content.rect.height);
            set
            {
                UpdateContentLayout(0, value.x);
                UpdateContentLayout(1, value.y);
            }
        }

        #endregion

        #region 布局参考

        [SerializeField] private LayoutGroup layoutGroup;

        private float _aamvUpdateTime;

        private Vector2 _lastContextSize;
        private Vector2 _lastContextAnchoredPosition;

        private List<GridLayoutCalculator.LayoutItem> _layoutItems;

        private Queue<RectTransform> _activeQueue = new Queue<RectTransform>();

        private bool layoutHasRebuildAtThisFrame;
        private bool layoutDirty;
        private bool indexMapDirty;

        private Vector2 visibleMin;
        private Vector2 visibleMax;

        #endregion

        [Serializable]
        public class ScrollItemData : IUIVesselVisibalControl
        {
            public GridLayoutCalculator.LayoutItem layout;
            public int index { get; internal set; }
            protected internal XericScrollRect parentScrollRect;
            protected bool _uiVisibal;

            protected GameObject item;
            public RectTransform itemRectTransform => item ? item.RectTransform() : null;

            /// <summary>
            /// 滚动框如何定位到接口（此接口返回的对象将用于生命周期管理，否则退化成简单的组件显影）
            /// </summary>
            public virtual IUIVesselVisibalControl ItemUIVesselVisibalControlComponent => this;

            public bool GetUIVisibal => _uiVisibal;

            public virtual void UnderVisible()
            {
                SetIsVisible(true);
                item = parentScrollRect.childrenPool.Get();
                RefreshPivot();
            }

            public virtual void OutOfVisible()
            {
                SetIsVisible(false);
                parentScrollRect.childrenPool.DirectlyRelease(item);
                item = null;
            }

            protected void SetIsVisible(bool visible)
            {
                _uiVisibal = visible;
            }

            protected void RefreshPivot()
            {
                var itemRect = itemRectTransform;
                if (!itemRect)
                {
                    Debug.LogError($"滚动视图的实体对象在刷新前并未指定，请先指定{nameof(item)}后再调用{nameof(RefreshPivot)}");
                }

                parentScrollRect.UpdateChildPivot(layout, itemRect);
                itemRect.anchoredPosition = layout.position;
                itemRect.SetDeltaSize(layout.size);
            }

            public void UpdateLayout(GridLayoutCalculator.LayoutItem layoutData)
            {
                layout = layoutData;
            }

            public Vector2 WorldAnchoredPosition(RectTransform parentContent)
            {
                // 获取对象的世界坐标位置
                var worldPos = itemRectTransform.TransformPoint(Vector3.zero);
                return worldPos;
            }
        }

        #region 生命周期

#if UNITY_EDITOR
        private Vector3[] vertex1 = new Vector3[4];
        private Vector3[] vertex2 = new Vector3[4];
        private Vector3[] vertex3 = new Vector3[4];

        private void OnDrawGizmosSelected()
        {
            var lossyScale = transform.lossyScale;
            var scale = lossyScale * m_preloadMargin;
            var dis2 = lossyScale * m_discernibleDistance;

            content.GetWorldCorners(vertex1);
            viewport.GetWorldCorners(vertex2);
            var rect = new Rect(vertex2[0], new Vector2(vertex2[2].x - vertex2[0].x, vertex2[2].y - vertex2[0].y));
            var viewportMin = rect.min * lossyScale;
            var viewportMax = rect.max * lossyScale;

            // vertex3[0] = new Vector3(vertex2[0].x - scale.x, vertex2[0].y - scale.y, vertex2[0].z); 
            // vertex3[1] = new Vector3(vertex2[1].x - scale.x, vertex2[1].y + scale.y, vertex2[1].z); 
            // vertex3[2] = new Vector3(vertex2[2].x + scale.x, vertex2[2].y + scale.y, vertex2[2].z); 
            // vertex3[3] = new Vector3(vertex2[3].x + scale.x, vertex2[3].y - scale.y, vertex2[3].z); 

            vertex3[0] = new Vector3(horizontal ? rect.min.x - scale.x : rect.min.x,
                vertical ? rect.min.y - scale.y : rect.min.y, vertex2[0].z);
            vertex3[1] = new Vector3(horizontal ? rect.min.x - scale.x : rect.min.x,
                vertical ? rect.max.y + scale.y : rect.max.y, vertex2[1].z);
            vertex3[2] = new Vector3(horizontal ? rect.max.x + scale.x : rect.max.x,
                vertical ? rect.max.y + scale.y : rect.max.y, vertex2[2].z);
            vertex3[3] = new Vector3(horizontal ? rect.max.x + scale.x : rect.max.x,
                vertical ? rect.min.y - scale.y : rect.min.y, vertex2[3].z);

            Gizmos.color = Color.yellow;
            MacroGizmosDraw.DrawGizmos2RectLinkCube(vertex3, vertex1, Color.cyan, Color.red, true);
            MacroGizmosDraw.Label(vertex3[1], "preload Rect");
            MacroGizmosDraw.Label(vertex1[1], "display Rect");

            if (!Application.isPlaying) return;

            Gizmos.color = Color.magenta;
            MacroGizmosDraw.DrawGizmosRectLine(visibleMin - Vector2.one, visibleMax - Vector2.one);

            var corners = new Vector3[4];
            foreach (var data in _childrenDatas)
            {
                if (data.GetUIVisibal)
                {
                    Gizmos.color = Color.green;
                    data.itemRectTransform.GetWorldCorners(corners);
                    MacroGizmosDraw.DrawPolygonLineByPointArray(corners);
                    MacroGizmosDraw.Label(corners[0], $"[{data.index}] 使用中");
                }
                else
                {
                    Gizmos.color = Color.red;
                    var worldPos = (Vector2)content.TransformPoint(data.layout.position);
                    MacroGizmosDraw.DrawGizmosRectLine(new Vector2(worldPos.x, worldPos.y - data.layout.size.y) , new Vector2(worldPos.x + data.layout.size.x, worldPos.y));
                    MacroGizmosDraw.Label(worldPos, $"[{data.index}] 已回收");
                }
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (childrenPool.Parent == null)
                childrenPool.Parent = content;
            RefreshLayout();
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

        protected override void Start()
        {
            base.Start();
            // childrenPool 是这个插件后来加的格：老预制体里根本没有这一项（加载后是空池），
            // 无条件 Initialize 会在 Start 里抛 ArgumentNullException。没配预制体就跳过——
            // 不用对象池的滚动列表照常滚动，只是没有池化子项。
            if (childrenPool != null && childrenPool.Prefab != null)
                childrenPool.Initialize();
        }

        protected override void SetContentAnchoredPosition(Vector2 position)
        {
            base.SetContentAnchoredPosition(position);
            // 如果不启动就直接跳过
            if (!m_enableAdvancedFunc) return;

            // 计算更新速率
            if (m_AAMVUpdateRate <= 0)
                Next();
            else if ((_aamvUpdateTime += Time.deltaTime) >= m_AAMVUpdateRate)
            {
                // var targetFrameRate = Application.targetFrameRate;
                _aamvUpdateTime %= m_AAMVUpdateRate;
                Next();
            }


            void Next()
            {
                // 记录移动矢量
                var vector = position - _lastContextAnchoredPosition;

                // 移动矢量距离大于可查觉距离
                if (vector.sqrMagnitude > m_discernibleDistance * m_discernibleDistance)
                {
                    // 只有距离大于这个才会触发更新
                    _lastContextAnchoredPosition = position;
                    UpdateAAMVSVisibleStatus();
                }
            }
        }

        protected override void LateUpdate()
        {
            layoutHasRebuildAtThisFrame = false;
            // 全部索引刷新
            if (indexMapDirty)
            {
                indexMapDirty = false;
                for (int i = 0; i < _childrenDatas.Count; i++)
                    _childrenDatas[i].index = i;
            }

            // 重新计算布局
            if (layoutDirty)
                RefreshLayout();
            base.LateUpdate();
            // 布局刷新
            if (layoutDirty)
            {
                layoutDirty = false;
                // 常规的刷新依赖视图的更新，如果这一帧只增删了对象，但是视图没有移动，那么这里就会执行更新
                if (!layoutHasRebuildAtThisFrame)
                    UpdateAAMVSVisibleStatus();
            }
        }

        private Vector3[] viewportVertex = new Vector3[4];

        /// <summary>
        /// 自动剔除更新标记
        /// </summary>
        protected void UpdateAAMVSVisibleStatus()
        {
            layoutHasRebuildAtThisFrame = true;
            // 将视口尺寸转换到content本地空间
            // 手动计算视口在content空间的矩形
            viewport.GetWorldCorners(viewportVertex);
            var preloadMarginVec2 = new Vector3(m_preloadMargin, m_preloadMargin);
            var viewportRect = new Rect(
                viewportVertex[0] - preloadMarginVec2,
                viewportVertex[2] - viewportVertex[0] + preloadMarginVec2 + preloadMarginVec2);

            // 扩展可见区域边界
            visibleMin = viewportRect.min;
            visibleMax = viewportRect.max;

            var contentRect = content.rect;
            // 计算预加载边距（考虑content缩放）
            // var scaledMargin = preloadMargin / ((content.lossyScale.x + content.lossyScale.y) * 0.5f);

            // 获取当前滚动归一化位置
            // var scrollPos = new Vector2(
            //     horizontal ? horizontalScrollbar?.value ?? 0 : 0,
            //     vertical ? 1 - (verticalScrollbar?.value ?? 1) : 0);

            foreach (var childData in _childrenDatas)
            {
                var childRectTransform = childData.itemRectTransform;
                if (childRectTransform)
                {
                    var visControl = childData.ItemUIVesselVisibalControlComponent;
                    var isVisControlValid = visControl != null;

                    // 直接使用anchoredPosition进行边界检测
                    var childPos = childData.WorldAnchoredPosition(content);
                    var isVisible = CheckIsVisibal(childPos);
                    var nowActive = childRectTransform.GetActivity();

                    // 以接口为准
                    if (isVisControlValid)
                        nowActive = visControl.GetUIVisibal;

                    // 现在的状态与实际一致，跳过
                    if (isVisible == nowActive)
                        continue;

                    // 向成员发送状态(如果成员没有实现接口，就直接通过可见性控制)
                    if (isVisControlValid)
                        visControl.CallVisible(isVisible);
                    else
                        childRectTransform.gameObject.SetActive(isVisible);
                }
                else
                {
                    // 将本地坐标转换为世界坐标进行比较
                    var worldPos = content.TransformPoint(childData.layout.position);
                    if (CheckIsVisibal(worldPos))
                        childData.UnderVisible();
                }
            }

            bool CheckIsVisibal(Vector2 pos) => viewportRect.Contains(pos);
        }

        #endregion

        #region 数据管理

        /// <summary>
        /// 添加一个子项（自行重写这个子项类型数据结构，基于其中的生命周期函数完成内容的更新）
        /// </summary>
        /// <param name="data"></param>
        public void AddChildData(ScrollItemData data)
        {
            layoutDirty = true;
            data.index = _childrenDatas.Count;
            data.parentScrollRect = this;
            _childrenDatas.Add(data);
        }

        /// <summary>
        /// 添加一个子项在倒序的多少项之前
        /// </summary>
        /// <param name="data"></param>
        /// <param name="invertedOrder">指定这个项目插入在倒序的第几位</param>
        /// <remarks>
        /// 比如一个视图中有不同的几种对象，如常规对象和新建对象按钮，将这个对象插入在新建对象之前
        /// </remarks>
        public void AddChildData(ScrollItemData data, int invertedOrder)
        {
            layoutDirty = true;
            indexMapDirty = true;
            data.parentScrollRect = this;

            var insertIndex = _childrenDatas.Count - invertedOrder;
            insertIndex = Mathf.Clamp(insertIndex, 0, _childrenDatas.Count);
            _childrenDatas.Insert(insertIndex, data);
            // for (int i = 0; i < _childrenDatas.Count; i++)
            //     _childrenDatas[i].index = i;
        }

        public void RemoveChildData(int index)
        {
            layoutDirty = true;
            indexMapDirty = true;
            // 如果可见，先移除可见性
            if (_childrenDatas[index].GetUIVisibal)
                _childrenDatas[index].OutOfVisible();
            _childrenDatas.RemoveAt(index);
        }

        public void RemoveChildData(ScrollItemData index)
        {
            layoutDirty = true;
            indexMapDirty = true;
            // 如果可见，先移除可见性
            if (index.GetUIVisibal)
                index.OutOfVisible();
            _childrenDatas.Remove(index);
        }

        public void ClearChildData()
        {
            layoutDirty = true;
            indexMapDirty = true;
            // 如果可见，先移除可见性
            foreach (var data in _childrenDatas)
                if (data.GetUIVisibal)
                    data.OutOfVisible();
            _childrenDatas.Clear();
        }

        public ScrollItemData GetChildData(int index)
        {
            return _childrenDatas[index];
        }

        #endregion

        #region 布局计算

        private void RefreshLayout()
        {
            Debug.Log("刷新");
            var layouts = GetChildrenLayout(_childrenDatas.Count);
            for (int i = 0; i < layouts.Count; i++)
            {
                var childData = _childrenDatas[i];
                childData.UpdateLayout(layouts[i]);

                var trans = childData.itemRectTransform;
                if (trans)
                {
                    UpdateChildPivot(layouts[i], trans); // 先更新枢轴
                    if (trans != null)
                    {
                        trans.anchoredPosition = layouts[i].position;
                        trans.SetDeltaSize(m_cellSize);
                    }
                }
            }

            RefreshContentSizeByChildrenLayouts(layouts);
        }

        private void UpdateContentLayout(int axis, float overrideSize)
        {
            content.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, overrideSize);
            // m_HorizontalFit == ContentSizeFitter.FitMode.MinSize ? 
            //     LayoutUtility.GetMinSize(content, axis) : 
            //     LayoutUtility.GetPreferredSize(content, axis));
        }

        private void RefreshContentSizeByChildrenLayouts(List<GridLayoutCalculator.LayoutItem> layoutItems)
        {
            if (layoutItems.Count == 0) return;
            var size = Vector2.zero;
            var spacing = new Vector2(
                GridLayoutCalculator.Grid.y * m_spacing.x, 
                GridLayoutCalculator.Grid.x * m_spacing.y);
            switch (m_startAxis)
            {
                case GridLayoutGroup.Axis.Horizontal:
                    size.x = Mathf.Max(spacing.x, viewport.rect.width);
                    size.y = Mathf.Max(spacing.y, m_cellSize.y);
                    break;
                case GridLayoutGroup.Axis.Vertical:
                    size.x = Mathf.Max(spacing.x, m_cellSize.x);
                    size.y = Mathf.Max(spacing.y, viewport.rect.height);
                    break;
            }

            ContextSize = size;
        }

        internal void UpdateChildPivot(GridLayoutCalculator.LayoutItem layoutItems, RectTransform rectTransform)
        {
            rectTransform.anchorMax = rectTransform.anchorMin = m_startCorner switch
            {
                // todo 对齐算法总的方向应该是写错了，找找
                GridLayoutGroup.Corner.UpperLeft => new Vector2(0, 1),
                GridLayoutGroup.Corner.UpperRight => new Vector2(1, 1),
                GridLayoutGroup.Corner.LowerLeft => new Vector2(0, 0),
                GridLayoutGroup.Corner.LowerRight => new Vector2(1, 0),
                _ => throw new ArgumentOutOfRangeException()
            };
            rectTransform.pivot = m_childAlignment switch
            {
                TextAnchor.UpperLeft => new Vector2(0, 1),
                TextAnchor.UpperCenter => new Vector2(.5f, 1),
                TextAnchor.UpperRight => new Vector2(1, 1),
                TextAnchor.MiddleLeft => new Vector2(0, .5f),
                TextAnchor.MiddleCenter => new Vector2(.5f, .5f),
                TextAnchor.MiddleRight => new Vector2(1, .5f),
                TextAnchor.LowerLeft => new Vector2(0, 0),
                TextAnchor.LowerCenter => new Vector2(.5f, 0),
                TextAnchor.LowerRight => new Vector2(1, 0),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void GetOffset(List<GridLayoutCalculator.LayoutItem> layoutItems)
        {
            var first = layoutItems[0];
            var last = layoutItems[^1];

            // first.position
        }

        private List<GridLayoutCalculator.LayoutItem> GetChildrenLayout(int childrenCount)
        {
            // 根据约束类型设置合适的 constraintCount
            // FixedColumnCount: 计算容器能容纳多少列 = 容器宽度 / (单元格宽度 + 间距)
            // FixedRowCount: 计算容器能容纳多少行 = 容器高度 / (单元格高度 + 间距)
            int constraintCount = m_constraint switch
            {
                GridLayoutGroup.Constraint.FixedColumnCount => 
                    Mathf.CeilToInt((ContextSize.x - m_padding.horizontal) / (m_cellSize.x + m_spacing.x)),
                GridLayoutGroup.Constraint.FixedRowCount => 
                    Mathf.CeilToInt((ContextSize.y - m_padding.vertical) / (m_cellSize.y + m_spacing.y)),
                _ => 0,
            };

            if (m_constraintCount > 0)
                constraintCount = m_constraintCount;

            return GridLayoutCalculator.CalculateGridLayout(
                ref _layoutItems,
                childCount: childrenCount,
                containerSize: ContextSize,
                cellSize: m_cellSize,
                spacing: m_spacing,
                padding: m_padding,
                startCorner: m_startCorner,
                startAxis: m_startAxis,
                constraint: m_constraint,
                constraintCount: constraintCount,
                childAlignment: m_childAlignment
            );
        }

        public void RefreshChildrenLayout()
        {
            GetChildrenLayout(_childrenDatas.Count);
            RefreshContentSizeByChildrenLayouts(_layoutItems);
            // 应用计算结果
            for (int i = 0; i < _layoutItems.Count; i++)
            {
                var item = _layoutItems[i];
                _childrenDatas[i].UpdateLayout(item);
                Debug.Log($"Item {i}: Position{item.position}, Row{item.rowIndex}, Column{item.columnIndex}");
            }
        }

        #endregion

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "ScrollRect/Replace To XericScrollView", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is ScrollRect and not XericScrollRect;

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "ScrollRect/Replace To XericScrollView", false, 10)]
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

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "ScrollRect/Replace To ScrollView", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericScrollRect;

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "ScrollRect/Replace To ScrollView", false, 10)]
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