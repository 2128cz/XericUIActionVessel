using System;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XericUI.Core.Base
{
    public class XericUIBehaciour : UIBehaviour
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
                if (_canvas == null)
                    _canvas = GetComponentInParent<Canvas>();
#if UNITY_EDITOR
                if (_canvas == null)
                    Debug.LogError($"{name} 无法找到布局组件，接下来的流程可能会引发错误", this);
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
                if (_canvasScaler == null)
                    _canvasScaler = GetComponentInParent<CanvasScaler>();
#if UNITY_EDITOR
                if (_canvas == null)
                    Debug.LogError($"{name} 无法找到布局缩放组件，接下来的流程可能会引发错误", this);
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
                if (_graphicRaycaster == null)
                    _graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
#if UNITY_EDITOR
                if (_canvas == null)
                    Debug.LogError($"{name} 无法找到布局射线检测，接下来的流程可能会引发错误", this);
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
                if (_rect == null)
                    _rect = GetComponent<RectTransform>();
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
        }

        #endregion
    }
}