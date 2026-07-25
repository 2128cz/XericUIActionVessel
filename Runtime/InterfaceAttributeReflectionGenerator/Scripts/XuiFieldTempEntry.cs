using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段 Entry 接口
    /// 数据对象字段与 UI 组件之间的中间缓存层
    /// </summary>
    public interface IFieldEntry
    {
        /// <summary>Entry 名称（字段名或编组路径）</summary>
        string EntryName { get; }

        /// <summary>Entry 完整路径（含父级路径）</summary>
        string EntryPath { get; }

        /// <summary>主要功能标记</summary>
        XuiavrgFieldTag Tag { get; }

        /// <summary>标记索引</summary>
        int TagIndex { get; }

        /// <summary>排序值（越小越靠前）</summary>
        int FieldOrder { get; set; }

        /// <summary>是否为编组 Entry（包含子 Entry）</summary>
        bool IsGroupEntry { get; }

        /// <summary>父 Entry</summary>
        IFieldEntry Parent { get; }

        /// <summary>子 Entry 列表（只读）</summary>
        IReadOnlyList<IFieldEntry> Children { get; }

        /// <summary>生成的 UI 实例引用</summary>
        GameObject UIInstance { get; }

        /// <summary>绑定的预制体引用</summary>
        GameObject BoundPrefab { get; set; }

        /// <summary>关联的 MVVM 绑定列表</summary>
        IReadOnlyList<AvrgMvvmBinding> Bindings { get; }

        // === 生命周期 ===

        /// <summary>Entry 创建时调用</summary>
        void OnEntryCreate();

        /// <summary>绑定 UI 和数据时调用</summary>
        void OnEntryBind();

        /// <summary>释放时调用（归还池前）</summary>
        void OnEntryRelease();

        /// <summary>销毁时调用</summary>
        void OnEntryDestroy();

        // === 绑定管理 ===

        /// <summary>添加 MVVM 绑定到此 Entry</summary>
        void AddBinding(AvrgMvvmBinding binding, AvrgMvvmScheduler scheduler);

        // === UI 绑定 ===

        /// <summary>
        /// 绑定 UI 组件
        /// </summary>
        void BindUIComponent(GameObject uiInstance, List<Component> componentCache);
    }

    /// <summary>
    /// 字段临时 Entry 实现类
    /// 作为数据对象和 UI 组件之间的中间缓存层
    /// 
    /// UI 交互事件的分配通过 XUIAvrgMDValue.BindToAccessor() 的多态机制完成，
    /// 各类 XUIAvrgMDValue 子类自行管理其专属 UI 组件的交互订阅。
    /// </summary>
    public class XuiFieldTempEntry : IFieldEntry
    {
        #region 对象池

        private static ObjectPool<XuiFieldTempEntry> _pool;
        internal static ObjectPool<XuiFieldTempEntry> Pool
        {
            get
            {
                if (_pool == null)
                {
                    _pool = new ObjectPool<XuiFieldTempEntry>(
                        createFunc: () => new XuiFieldTempEntry(),
                        actionOnGet: entry => entry.OnEntryCreate(),
                        actionOnRelease: entry => entry.OnEntryRelease(),
                        actionOnDestroy: entry => entry.OnEntryDestroy(),
                        collectionCheck: false,
                        defaultCapacity: 32,
                        maxSize: 512
                    );
                }
                return _pool;
            }
        }

        /// <summary>从池中获取 Entry 实例</summary>
        public static XuiFieldTempEntry Get() => Pool.Get();

        /// <summary>将 Entry 归还池中</summary>
        public static void Release(XuiFieldTempEntry entry) => Pool.Release(entry);

        #endregion

        #region 字段

        private string _entryName;
        private string _entryPath;
        private IFieldEntry _parent;
        private List<XuiFieldTempEntry> _children = new List<XuiFieldTempEntry>();
        private List<AvrgMvvmBinding> _bindings = new List<AvrgMvvmBinding>();
        private AvrgMvvmScheduler _scheduler;
        private GameObject _uiInstance;
        private GameObject _boundPrefab;
        private int _fieldOrder;
        private bool _uiBound;

        #endregion

        #region IFieldEntry 属性实现

        public string EntryName => _entryName;

        public string EntryPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_entryPath)) return _entryPath;
                if (_parent != null)
                {
                    string parentPath = _parent.EntryPath;
                    return string.IsNullOrEmpty(parentPath)
                        ? _entryName
                        : parentPath + "/" + _entryName;
                }
                return _entryName;
            }
        }

        public XuiavrgFieldTag Tag { get; private set; }
        public int TagIndex { get; private set; }

        public int FieldOrder
        {
            get => _fieldOrder;
            set => _fieldOrder = value;
        }

        public bool IsGroupEntry => _children.Count > 0;

        public IFieldEntry Parent => _parent;

        public IReadOnlyList<IFieldEntry> Children => _children;

        public GameObject UIInstance => _uiInstance;

        public GameObject BoundPrefab
        {
            get => _boundPrefab;
            set => _boundPrefab = value;
        }

        public IReadOnlyList<AvrgMvvmBinding> Bindings => _bindings;

        #endregion

        #region 初始化方法

        public void Initialize(string entryName, XuiavrgFieldTag tag = XuiavrgFieldTag.None, int tagIndex = 0)
        {
            _entryName = entryName;
            _entryPath = null;
            Tag = tag;
            TagIndex = tagIndex;
            _fieldOrder = 0;
            _uiBound = false;
        }

        public void SetParent(IFieldEntry parent)
        {
            _parent = parent;
            _entryPath = null;
        }

        public void AddChild(XuiFieldTempEntry child)
        {
            if (child == null) return;
            child.SetParent(this);
            _children.Add(child);
        }

        public void AddBinding(AvrgMvvmBinding binding, AvrgMvvmScheduler scheduler)
        {
            if (binding == null) return;
            _bindings.Add(binding);
            _scheduler = scheduler;

            if (Tag == XuiavrgFieldTag.None && binding.MetaData?.Tag != XuiavrgFieldTag.None)
            {
                Tag = binding.MetaData.Tag;
                TagIndex = binding.MetaData.TagIndex;
            }
        }

        #endregion

        #region UI 绑定

        public void BindUIComponent(GameObject uiInstance, List<Component> componentCache)
        {
            _uiInstance = uiInstance;
            if (_uiInstance == null || _uiBound) return;

            if (componentCache == null)
            {
                componentCache = new List<Component>();
                _uiInstance.GetComponentsInChildren(true, componentCache);
            }

            // 遍历组件缓存，寻找 XUIAvrgMDValue 按 Tag 匹配
            foreach (var comp in componentCache)
            {
                if (comp is XUIAvrgMDValue mdValue)
                {
                    if (mdValue.EntryTag == this.Tag &&
                        (this.TagIndex == 0 || mdValue.TagIndex == this.TagIndex))
                    {
                        BindComponentWithBinding(mdValue);
                    }
                }
            }

            // 子 Entry 复用组件缓存
            foreach (var child in _children)
            {
                child.BindUIComponent(_uiInstance, componentCache);
            }

            _uiBound = true;
        }

        /// <summary>
        /// 将 XUIAvrgMDValue 与 MVVM 绑定连接
        /// 通过多态 BindToAccessor 让 UI 子类自行管理交互事件和显示更新
        /// </summary>
        private void BindComponentWithBinding(XUIAvrgMDValue mdValue)
        {
            if (_bindings.Count == 0) return;

            var primaryBinding = _bindings[0];

            // ★ 多态调用: UI 子类自行处理交互事件和显示更新
            mdValue.BindToAccessor(primaryBinding.Accessor, primaryBinding, _scheduler);

            // 拆分属性子绑定处理
            if (primaryBinding.SubBindings?.Count > 0 && mdValue.AvailableBindingSlots?.Count > 0)
            {
                var childMDValues = new List<XUIAvrgMDValue>();
                mdValue.GetComponentsInChildren(true, childMDValues);

                foreach (var slot in mdValue.AvailableBindingSlots)
                {
                    var subBinding = primaryBinding.SubBindings.Find(
                        sb => sb.MetaData?.PropertyPath == slot.propertyName);
                    if (subBinding == null) continue;

                    foreach (var childMD in childMDValues)
                    {
                        if (childMD == mdValue) continue;
                        if (childMD.EntryTag == slot.tag || slot.tag == XuiavrgFieldTag.None)
                        {
                            childMD.BindToAccessor(subBinding.Accessor, subBinding, _scheduler);
                            childMD.InitializeWithMetaData(subBinding.MetaData);
                            break;
                        }
                    }
                }
            }

            // 广播主元数据到样式组件
            mdValue.InitializeWithMetaData(primaryBinding.MetaData);
        }

        #endregion

        #region 生命周期

        public void OnEntryCreate()
        {
            _entryName = null;
            _entryPath = null;
            _parent = null;
            _fieldOrder = 0;
            _uiBound = false;
        }

        public void OnEntryBind()
        {
            foreach (var binding in _bindings)
            {
                var value = binding.ReadFromDataSource();
                binding.NotifyUIUpdate(value, ValidationState.Normal);

                if (binding.SubBindings?.Count > 0)
                {
                    foreach (var subBinding in binding.SubBindings)
                    {
                        var subValue = subBinding.ReadFromDataSource();
                        subBinding.NotifyUIUpdate(subValue, ValidationState.Normal);
                    }
                }
            }
        }

        public void OnEntryRelease()
        {
            _entryName = null;
            _entryPath = null;
            _parent = null;
            _uiInstance = null;
            _boundPrefab = null;
            Tag = XuiavrgFieldTag.None;
            TagIndex = 0;
            _fieldOrder = 0;
            _uiBound = false;

            foreach (var binding in _bindings)
            {
                binding.ClearUIEvent();
                if (binding.SubBindings?.Count > 0)
                {
                    foreach (var sub in binding.SubBindings)
                        sub.ClearUIEvent();
                }
            }
            _bindings.Clear();
            _scheduler = null;

            foreach (var child in _children)
            {
                Release(child);
            }
            _children.Clear();
        }

        public void OnEntryDestroy()
        {
            foreach (var binding in _bindings)
            {
                binding.ClearUIEvent();
                if (binding.SubBindings?.Count > 0)
                {
                    foreach (var sub in binding.SubBindings)
                        sub.ClearUIEvent();
                }
            }
            _bindings.Clear();
            _children.Clear();
            _uiInstance = null;
            _scheduler = null;
        }

        #endregion
    }
}
