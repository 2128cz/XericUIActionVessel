using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericLibrary.Runtime.Type.SpatialAlgorithm;
using XericLibraryEditor.Debug;
using XericUI.Core.Base;
using XericUI.OverrideLayout;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace XericUI.DynamicActivitiesImpact
{
    /// <summary>
    /// 导入动态激活组件(需要外部控制部分检查与生命周期事件)
    /// </summary>
    [Obsolete("直接用XericScrollRect")]
    public class UIDynamicActiveImpact : XericUIBehaciour
    {
        #region 字段属性
        
#if ODIN_INSPECTOR
        [LabelText("主要容器")]
        [BoxGroup("基本容器", centerLabel: true)]
#endif
        [Tooltip("容器子集应呈现平摊的样式")] 
        public RectTransform containerTransform = null;

#if ODIN_INSPECTOR
        [BoxGroup("基本容器")]
        [LabelText("遮罩容器")]
#endif
        [Tooltip("用作遮罩计算的组件")] 
        public RectTransform maskRectTransform = null;

#if ODIN_INSPECTOR
        [BoxGroup("基本容器")]
        [LabelText("容器排版控制")]
#endif  
        public LayoutGroup layoutGroup;

      
#if ODIN_INSPECTOR
        [BoxGroup("容器性能")]
        [LabelText("预加载像素范围")]
#endif
        public float preloadMargin = 200f;
        
#if ODIN_INSPECTOR
        [BoxGroup("容器性能")]
        [LabelText("可被察觉的响应变化长度")]
#endif  
        public float discernibleDistance = 10f;
        
#if ODIN_INSPECTOR
        [BoxGroup("容器性能")]
        [LabelText("每秒检查更新的次数")]
#endif  
        [Range(0.1f, 50)]
        public float updateRateLimit = 2;

        #endregion

        #region 容器成员

        internal bool _layoutDirty;
        internal bool _transformDirty;
        internal int _lastChildrenCount;
        internal Vector2 _lastPosition;
        internal HashSet<RectTransform> _activeSet = null;
        internal HashSet<RectTransform> _allObjsSet = null;
        
        private HashSetPool<RectTransform> _tempQuadUIHashSetPool = new HashSetPool<RectTransform>();
        
        /// <summary>
        /// 保存所有ui对象的四叉树
        /// </summary>
        protected QuadTree<RectTransform> QuadUITargetTree;

        
        public virtual IEnumerable<RectTransform> ContainerChildren => containerTransform.GetChildren().Select(a => a.RectTransform());
        public virtual int ContainerChildrenCount => containerTransform.childCount; 
        public virtual Rect ContainerTransformRectRange => containerTransform.ToQuadTreeRect();
        public virtual Rect MaskRectTransformRectRange => maskRectTransform.ToQuadTreeRect();
        public bool IsLayoutDirty => _layoutDirty;
        public bool IsTransformDirty => _transformDirty;
        public float SaftyDiscernibleUpdateTime => Mathf.Max(1f / 60, 1f / updateRateLimit);
        
        public LayoutGroup MainLayoutGroup => transform.GetComponent<LayoutGroup>();
        
        #endregion
        
        #region 生命周期    

#if UNITY_EDITOR
        private Vector3[] vertex1 = new Vector3[4];
        private Vector3[] vertex2 = new Vector3[4];
        private Vector3[] vertex3 = new Vector3[4];
        private void OnDrawGizmosSelected()
        {
            var lossyScale = rectTransform.lossyScale;
            var scale = lossyScale * preloadMargin;
            var dis2 = lossyScale * discernibleDistance;
            
            containerTransform.GetWorldCorners(vertex1);

            vertex2[0] = new Vector3(vertex1[0].x - scale.x, vertex1[0].y - scale.y, vertex1[0].z); 
            vertex2[1] = new Vector3(vertex1[1].x - scale.x, vertex1[1].y + scale.y, vertex1[1].z); 
            vertex2[2] = new Vector3(vertex1[2].x + scale.x, vertex1[2].y + scale.y, vertex1[2].z); 
            vertex2[3] = new Vector3(vertex1[3].x + scale.x, vertex1[3].y - scale.y, vertex1[3].z); 
            
            vertex3[0] = new Vector3(vertex1[0].x - dis2.x, vertex1[0].y - dis2.y, vertex1[0].z); 
            vertex3[1] = new Vector3(vertex1[1].x - dis2.x, vertex1[1].y + dis2.y, vertex1[1].z); 
            vertex3[2] = new Vector3(vertex1[2].x + dis2.x, vertex1[2].y + dis2.y, vertex1[2].z); 
            vertex3[3] = new Vector3(vertex1[3].x + dis2.x, vertex1[3].y - dis2.y, vertex1[3].z); 
                
            Gizmos.color = Color.yellow;
            MacroGizmosDraw.DrawPolygonLineByPointArray(vertex3);
            MacroGizmosDraw.DrawGizmosRectLinkLine(vertex1, vertex2, Color.grey, Color.cyan, true);

        }
#endif

        protected override void Awake()
        {
            base.Awake();
            // 容器
            if (containerTransform == null)
                containerTransform = rectTransform;
            // 遮罩
            if (maskRectTransform == null)
            {
                var mask = transform.GetComponentInParent<Mask>();
                maskRectTransform = mask.RectTransform();
            }
            // 布局
            if (layoutGroup == null)
                layoutGroup = transform.GetComponentInParent<LayoutGroup>();
            if (layoutGroup is XericHorizontalOrVerticalLayoutGroup xericLayoutGroup)
            {
                xericLayoutGroup.layoutIgnoreInactive = false;
            }
            
            CheckIsDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        private float accTime = 0;
        
        protected virtual void Update()
        {
            if (accTime < SaftyDiscernibleUpdateTime)
            {
                accTime += Time.unscaledDeltaTime;
                return;
            }
            // 毕竟这里不是粒子计算器
            accTime = accTime % SaftyDiscernibleUpdateTime;
            
            if (IsLayoutDirty)
            {
                ResetDirtyFlag();
                RebuildQurdTree();
            }
            else if (IsTransformDirty)
            {
                ResetDirtyFlag();
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            CheckIsDirty();
        }
        
        private void OnDestroy()
        {
            // 恢复所有元素的原始状态
            foreach (var element in _allObjsSet)
            {
                if (element != null && _activeSet.Contains(element))
                {
                    element.gameObject.SetActive(true);
                }
            }
        }

        #endregion

        #region 刷新框架

        /// <summary>
        /// 设置布局脏
        /// </summary>
        public void LayoutDirty()
            => _layoutDirty = true;

        /// <summary>
        /// 设置变换脏
        /// </summary>
        public void TransformDirty()
            => _transformDirty = true;

        /// <summary>
        /// 重置脏标记
        /// </summary>
        private void ResetDirtyFlag()
        {
            _layoutDirty = false;
            _transformDirty = false;
        }
        
        /// <summary>
        /// 检查容器脏
        /// </summary>
        public virtual void CheckIsDirty()
        {
            if (QuadUITargetTree == null || // 四叉树不存在？
                _lastChildrenCount != ContainerChildrenCount // 成员数量明显不对
                )
            {
                LayoutDirty();
                BackupQuadRectValue();
                return;
            }
            
            // todo 这里暂时只简单考虑直来直去的移动方式，如果有其他复杂移动，需要修改判别条件
            var deltaPosIsDirty = Mathf.Abs(QuadUITargetTree.RootRect.position.sqrMagnitude - _lastPosition.sqrMagnitude) < discernibleDistance;
            
            if (deltaPosIsDirty // 发生不可忽视的变换？
                ) 
            {
                TransformDirty();
                BackupQuadRectValue();
                return;
            }
        }

        /// <summary>
        /// 重建四叉树
        /// </summary>
        public void RebuildQurdTree()
        {
            // 重建四叉树
            QuadUITargetTree = new QuadTree<RectTransform>(ContainerTransformRectRange, a => a.rect);
            // 记录参数
            BackupQuadRectValue();
        }

        /// <summary>
        /// 保存自动重建四叉树所需的参数
        /// </summary>
        /// <param name="target"></param>
        protected virtual void BackupQuadRectValue()
        {
            _lastPosition = ContainerTransformRectRange.position;
            _lastChildrenCount = ContainerChildrenCount;
        }

        
#if ODIN_INSPECTOR
        [Button("更新所有可见性")]
#endif
        /// <summary>
        /// 更新所有成员的可见性
        /// </summary>
        public virtual void UpdateAllObjectVisible()
        {
            if (QuadUITargetTree == null)
            {
                Debug.LogError($"在更新成员可见性之前，需要确保四叉树存在！");
                return;
            }

            var visibalSet = QuadUITargetTree.Retrieve(MaskRectTransformRectRange);
            ContainerChildren.ForEachDo(obj =>
            {
                obj.gameObject.SetActive(visibalSet.Contains(obj));
            });
        }

        /// <summary>
        /// 要求控制布局组的激活一次，提供为何激活的原因
        /// </summary>
        /// <param name="hasIncreased">由于新增成员导致布局更新</param>
        /// <param name="hasDecreased">由于销毁成员导致布局更新</param>
        public virtual void UpdateLayoutGroupOnce(bool hasIncreased, bool hasDecreased)
        {
            MainLayoutGroup.enabled = true;
            StartCoroutine(WaitForLayoutDisable());
            return;
            IEnumerator WaitForLayoutDisable()
            {
                if (CanvasUpdateRegistry.IsRebuildingLayout())
                    yield return new WaitForEndOfFrame();
                else
                    yield return new WaitForFixedUpdate();
                MainLayoutGroup.enabled = false;
            }
        }
        
        #endregion
    }
}

    
