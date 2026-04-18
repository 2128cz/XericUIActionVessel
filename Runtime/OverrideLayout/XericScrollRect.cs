using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
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
    [AddComponentMenu("Xeric UI Vessel/ScrollRect", 50)]
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class XericScrollRect : ScrollRect
    {
        #region 字段属性

        [Tooltip("启用自动剔除功能")] [SerializeField] 
        protected bool m_enableAdvancedFunc = true;

        [Tooltip("容器预加载像素范围")] [SerializeField]
        protected float m_preloadMargin = 200f;

        [Tooltip("容器加载响应可被察觉的变化长度")] [SerializeField]
        protected float m_discernibleDistance = 10f;

        [Tooltip("自动剔除更新速率，当设定趋近零时，实际上是期望每帧执行更新")] [SerializeField]
        protected float m_AAMVUpdateRate = .5f;

        // grid布局组件
        [SerializeField] protected Vector2 m_cellSize = new Vector2(100, 100);
        [SerializeField] protected Vector2 m_spacing = Vector2.zero;
        [SerializeField] protected RectOffset m_padding;
        [SerializeField] protected GridLayoutGroup.Corner m_startCorner = GridLayoutGroup.Corner.UpperLeft;
        [SerializeField] protected GridLayoutGroup.Axis m_startAxis = GridLayoutGroup.Axis.Horizontal;
        [SerializeField] protected TextAnchor m_childAlignment = TextAnchor.UpperLeft;
        [SerializeField] protected GridLayoutGroup.Constraint m_constraint = GridLayoutGroup.Constraint.Flexible;

        /// <summary>
        /// 对象池
        /// </summary>
        [SerializeField] protected MacroPool.UnionSet childrenPool = new MacroPool.UnionSet();

        /// <summary>
        /// 对象数据
        /// </summary>
        [SerializeField] private readonly List<ScrollItemData> _childrenDatas = ListPool<ScrollItemData>.Get();

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
        
        #endregion

        [Serializable]
        public class ScrollItemData : IUIVesselVisibalControl
        {
            public GridLayoutCalculator.LayoutItem layout;

            protected internal XericScrollRect parentScrollRect;
            private bool _uiVisibal;

            protected GameObject item;
            
            public bool GetUIVisibal => _uiVisibal;

            public virtual void UnderVisibal()
            {
                item = parentScrollRect.childrenPool.Get();
                var rectTransform = item.RectTransform();
                parentScrollRect.UpdateChildPivot(layout, item.RectTransform());
                rectTransform.anchoredPosition = layout.position;
            }

            public virtual void OutOfVisibal()
            { 
                parentScrollRect.childrenPool.DirectlyRelease(item);
            }

            public void UpdateLayout(GridLayoutCalculator.LayoutItem layoutData)
            {
                layout = layoutData;
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
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (childrenPool.Parent == null)
                childrenPool.Parent = content;

            var layouts = GetChildrenLayout(content.transform.childCount);
            for (int i = 0; i < layouts.Count; i++)
            {
                var trans = content.transform.GetChild(i) as RectTransform;
                UpdateChildPivot(layouts[i], trans);
                trans.anchoredPosition = layouts[i].position;
                trans.SetDeltaSize(m_cellSize);
            }

            RefreshContentSizeByChildrenLayouts(layouts);
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
                    UpdateAAMVSVisibleStatus(position);
                }
            }
        }

        private Vector3[] viewportVertex = new Vector3[4];

        /// <summary>
        /// 自动剔除更新标记
        /// </summary>
        protected void UpdateAAMVSVisibleStatus(Vector2 position)
        {
            // 将视口尺寸转换到content本地空间
            // 手动计算视口在content空间的矩形
            viewport.GetWorldCorners(viewportVertex);
            var viewportRect = new Rect(
                content.InverseTransformPoint(viewportVertex[0]),
                content.InverseTransformPoint(viewportVertex[2]) - content.InverseTransformPoint(viewportVertex[0]));

            var contentRect = content.rect;
            // 计算预加载边距（考虑content缩放）
            // var scaledMargin = preloadMargin / ((content.lossyScale.x + content.lossyScale.y) * 0.5f);

            // 扩展可见区域边界
            var visibleMin = viewportRect.min - new Vector2(m_preloadMargin, m_preloadMargin);
            var visibleMax = viewportRect.max + new Vector2(m_preloadMargin, m_preloadMargin);

            // 获取当前滚动归一化位置
            // var scrollPos = new Vector2(
            //     horizontal ? horizontalScrollbar?.value ?? 0 : 0,
            //     vertical ? 1 - (verticalScrollbar?.value ?? 1) : 0);

            // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
            foreach (RectTransform child in content.GetChildren())
            {
                var visControl = child.GetComponent<IUIVesselVisibalControl>();
                var isVisControlValid = visControl != null;

                var rectTransform = child.RectTransform();
                // 直接使用anchoredPosition进行边界检测
                var childPos = rectTransform.anchoredPosition;
                var isVisible =
                    childPos.x >= visibleMin.x &&
                    childPos.x <= visibleMax.x &&
                    childPos.y >= visibleMin.y &&
                    childPos.y <= visibleMax.y;

                var nowActive = child.GetActivity();

                // 以接口为准
                if (isVisControlValid)
                    nowActive = visControl.GetUIVisibal;

                // 现在的状态与实际一致，跳过
                if (isVisible == nowActive)
                    continue;

                // 向成员发送状态(如果成员没有实现接口，就直接通过可见性控制)
                if (isVisControlValid)
                    visControl.CallVisibal(isVisible);
                else
                    child.gameObject.SetActive(isVisible);
            }
        }

        #endregion

        #region 数据管理

        public T GetScrollItemData<T>()
            where T : ScrollItemData, new()
        {
            var result = new T
            {
                parentScrollRect = this
            };
            return result;
        }

        public void AddChildData(ScrollItemData Data)
        {
            _childrenDatas.Add(Data);
        }

        public void RemoveChildData(int index)
        {
        }

        public void RemoveChildData(ScrollItemData index)
        {
        }

        public ScrollItemData GetChildData(int index)
        {
            return _childrenDatas[index];
        }

        #endregion

        #region 布局计算

        private void UpdateContentLayout(int axis, float overrideSize)
        {
            content.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, overrideSize);
            // m_HorizontalFit == ContentSizeFitter.FitMode.MinSize ? 
            //     LayoutUtility.GetMinSize(content, axis) : 
            //     LayoutUtility.GetPreferredSize(content, axis));
        }

        private void RefreshContentSizeByChildrenLayouts(List<GridLayoutCalculator.LayoutItem> layoutItems)
        {
            var first = layoutItems[0];
            var last = layoutItems[^1];

            var size = first.position - last.position;
            size = size.Abs();
            var spacing = new Vector2(GridLayoutCalculator.Grid.x * m_spacing.x, GridLayoutCalculator.Grid.y * m_spacing.y);
            switch (m_startAxis)
            {
                case GridLayoutGroup.Axis.Horizontal:
                    var ySpacing = GridLayoutCalculator.Grid.y * m_spacing.y;
                    size.x = Mathf.Max(size.x, viewport.rect.width);
                    size.y = Mathf.Max(size.y + spacing.y, m_cellSize.y);
                    break;
                case GridLayoutGroup.Axis.Vertical:
                    var xSpacing = GridLayoutCalculator.Grid.x * m_spacing.x;
                    size.x = Mathf.Max(size.x + spacing.x, m_cellSize.x);
                    size.y = Mathf.Max(size.y, viewport.rect.height); 
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
                GridLayoutGroup.Corner.UpperRight =>  new Vector2(1, 1),
                GridLayoutGroup.Corner.LowerLeft => new Vector2(0, 0),
                GridLayoutGroup.Corner.LowerRight => new Vector2(1, 0),
                _ => throw new ArgumentOutOfRangeException()
            };
            // rectTransform.pivot = m_childAlignment switch
            // {
            //     TextAnchor.UpperLeft => new Vector2(0, 1),
            //     TextAnchor.UpperCenter => new Vector2(.5f, 1),
            //     TextAnchor.UpperRight => new Vector2(1, 1),
            //     TextAnchor.MiddleLeft => new Vector2(0, .5f),
            //     TextAnchor.MiddleCenter => new Vector2(.5f, .5f),
            //     TextAnchor.MiddleRight => new Vector2(1, .5f),
            //     TextAnchor.LowerLeft => new Vector2(0, 0),
            //     TextAnchor.LowerCenter => new Vector2(.5f, 0),
            //     TextAnchor.LowerRight => new Vector2(1, 0),
            //     _ => throw new ArgumentOutOfRangeException()
            // };
        }

        private void GetOffset(List<GridLayoutCalculator.LayoutItem> layoutItems)
        {
            var first = layoutItems[0];
            var last = layoutItems[^1];
            
            // first.position
        }
        
        private List<GridLayoutCalculator.LayoutItem> GetChildrenLayout(int childrenCount)
            => GridLayoutCalculator.CalculateGridLayout(
                _layoutItems,
                childCount: childrenCount,
                containerSize: ContextSize,
                cellSize: m_cellSize,
                spacing: m_spacing,
                padding: m_padding,
                startCorner: m_startCorner,
                startAxis: m_startAxis,
                constraint: m_constraint,
                constraintCount: 0,
                childAlignment: m_childAlignment
            );

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