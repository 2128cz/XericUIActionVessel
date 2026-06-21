using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// UI 字段标记提供者接口
    /// 所有 Tag 增强的 UI 组件需实现此接口，便于 Entry 在绑定阶段自动匹配
    /// </summary>
    public interface IXuiavrgFieldTagProvider
    {
        XuiavrgFieldTag EntryTag { get; }
        int TagIndex { get; }
    }

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

        /// <summary>绑定的预制体组</summary>
        XuiavrgPropertyGroupScriptableObject BoundPrefabGroup { get; set; }

        /// <summary>覆盖的预制体组名称</summary>
        string PrefabOverrideName { get; set; }

        // === 生命周期 ===

        /// <summary>Entry 创建时调用</summary>
        void OnEntryCreate();

        /// <summary>绑定 UI 和数据时调用</summary>
        void OnEntryBind();

        /// <summary>释放时调用（归还池前）</summary>
        void OnEntryRelease();

        /// <summary>销毁时调用</summary>
        void OnEntryDestroy();

        // === 数据访问 ===

        /// <summary>获取指定成员的数据值</summary>
        object GetDataValue(string memberName);

        /// <summary>设置指定成员的数据值</summary>
        void SetDataValue(string memberName, object value);

        /// <summary>获取 Entry 下所有绑定成员的名称</summary>
        IEnumerable<string> GetMemberNames();

        // === UI 绑定 ===

        /// <summary>
        /// 绑定 UI 组件
        /// </summary>
        /// <param name="uiInstance">UI 预制体实例</param>
        /// <param name="componentCache">
        /// 组件缓存列表。传入 null 时会自动收集 uiInstance 上所有 Component。
        /// 父 Entry 可将已收集的缓存传递给子 Entry 以提升性能
        /// </param>
        void BindUIComponent(GameObject uiInstance, List<Component> componentCache);
    }

    /// <summary>
    /// 字段临时 Entry 实现类
    /// 作为数据对象字段和 UI 组件之间的中间缓存层
    /// 
    /// 支持：
    /// - 多字段合并为一个 Entry（通过 XuiavrgFieldEntryGroupAttribute）
    /// - 嵌套 Entry（父 Entry 包含子 Entry）
    /// - 对象池复用（通过静态 ObjectPool）
    /// - MVVM 双向绑定的生命周期
    /// </summary>
    public class XuiFieldTempEntry : IFieldEntry
    {
        #region 对象池

        internal static ObjectPool<XuiFieldTempEntry> Pool { get; private set; }

        static XuiFieldTempEntry()
        {
            Pool = new ObjectPool<XuiFieldTempEntry>(
                createFunc: () => new XuiFieldTempEntry(),
                actionOnGet: entry => entry.OnEntryCreate(),
                actionOnRelease: entry => entry.OnEntryRelease(),
                actionOnDestroy: entry => entry.OnEntryDestroy(),
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: 512
            );
        }

        /// <summary>从池中获取 Entry 实例</summary>
        public static XuiFieldTempEntry Get() => Pool.Get();

        /// <summary>将 Entry 归还池中</summary>
        public static void Release(XuiFieldTempEntry entry) => Pool.Release(entry);

        #endregion

        #region 内部绑定数据结构

        /// <summary>
        /// 成员反射绑定信息
        /// </summary>
        internal class MemberBinding
        {
            /// <summary>绑定的数据目标对象</summary>
            public object valueTarget;

            /// <summary>是否为属性（false 为字段）</summary>
            public bool isProperty;

            /// <summary>属性信息</summary>
            public PropertyInfo propertyInfo;

            /// <summary>字段信息</summary>
            public FieldInfo fieldInfo;

            /// <summary>功能标记</summary>
            public XuiavrgFieldTag tag;

            /// <summary>标记索引</summary>
            public int tagIndex;

            /// <summary>
            /// 从绑定目标获取值
            /// </summary>
            public object GetValue()
            {
                if (valueTarget == null) return null;
                return isProperty
                    ? propertyInfo.GetValue(valueTarget)
                    : fieldInfo.GetValue(valueTarget);
            }

            /// <summary>
            /// 设置值到绑定目标
            /// </summary>
            public void SetValue(object value)
            {
                if (valueTarget == null) return;
                if (isProperty)
                {
                    if (propertyInfo.CanWrite)
                        propertyInfo.SetValue(valueTarget, value);
                }
                else
                {
                    fieldInfo.SetValue(valueTarget, value);
                }
            }
        }

        #endregion

        #region 字段

        private string _entryName;
        private string _entryPath;
        private IFieldEntry _parent;
        private List<XuiFieldTempEntry> _children = new List<XuiFieldTempEntry>();
        private Dictionary<string, MemberBinding> _bindings = new Dictionary<string, MemberBinding>();
        private GameObject _uiInstance;
        private XuiavrgPropertyGroupScriptableObject _boundPrefabGroup;
        private string _prefabOverrideName;
        private int _fieldOrder;

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

        public XuiavrgPropertyGroupScriptableObject BoundPrefabGroup
        {
            get => _boundPrefabGroup;
            set => _boundPrefabGroup = value;
        }

        public string PrefabOverrideName
        {
            get => _prefabOverrideName;
            set => _prefabOverrideName = value;
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 设置 Entry 基本属性
        /// </summary>
        public void Initialize(string entryName, XuiavrgFieldTag tag = XuiavrgFieldTag.None, int tagIndex = 0)
        {
            _entryName = entryName;
            _entryPath = null;
            Tag = tag;
            TagIndex = tagIndex;
            _fieldOrder = 0;
        }

        /// <summary>
        /// 设置父 Entry（用于嵌套）
        /// </summary>
        public void SetParent(IFieldEntry parent)
        {
            _parent = parent;
            _entryPath = null; // 重置路径，下次访问重新计算
        }

        /// <summary>
        /// 添加子 Entry
        /// </summary>
        public void AddChild(XuiFieldTempEntry child)
        {
            if (child == null) return;
            child.SetParent(this);
            _children.Add(child);
        }

        /// <summary>
        /// 注册成员绑定
        /// </summary>
        /// <param name="memberName">成员名称</param>
        /// <param name="memberInfo">反射信息</param>
        /// <param name="target">数据目标对象</param>
        /// <param name="tag">功能标记</param>
        /// <param name="tagIndex">标记索引</param>
        public void RegisterBinding(string memberName, MemberInfo memberInfo, object target,
            XuiavrgFieldTag tag = XuiavrgFieldTag.None, int tagIndex = 0)
        {
            MemberBinding binding = new MemberBinding
            {
                valueTarget = target,
                tag = tag,
                tagIndex = tagIndex
            };

            switch (memberInfo)
            {
                case PropertyInfo propInfo:
                    binding.isProperty = true;
                    binding.propertyInfo = propInfo;
                    break;
                case FieldInfo fieldInfo:
                    binding.isProperty = false;
                    binding.fieldInfo = fieldInfo;
                    break;
            }

            _bindings[memberName] = binding;

            // 如果是第一个注册的绑定，设置主 Tag
            if (Tag == XuiavrgFieldTag.None && tag != XuiavrgFieldTag.None)
            {
                Tag = tag;
                TagIndex = tagIndex;
            }
        }

        #endregion

        #region 数据访问

        public object GetDataValue(string memberName)
        {
            if (_bindings.TryGetValue(memberName, out var binding))
                return binding.GetValue();

            // 查找子 Entry
            foreach (var child in _children)
            {
                var value = child.GetDataValue(memberName);
                if (value != null) return value;
            }

            return null;
        }

        public void SetDataValue(string memberName, object value)
        {
            if (_bindings.TryGetValue(memberName, out var binding))
            {
                binding.SetValue(value);
                return;
            }

            // 查找子 Entry
            foreach (var child in _children)
            {
                if (child.HasBinding(memberName))
                {
                    child.SetDataValue(memberName, value);
                    return;
                }
            }
        }

        public IEnumerable<string> GetMemberNames() => _bindings.Keys;

        /// <summary>
        /// 检查是否包含指定成员绑定
        /// </summary>
        public bool HasBinding(string memberName) => _bindings.ContainsKey(memberName);

        /// <summary>
        /// 获取指定成员的标记
        /// </summary>
        public XuiavrgFieldTag GetMemberTag(string memberName)
        {
            if (_bindings.TryGetValue(memberName, out var binding))
                return binding.tag;
            return XuiavrgFieldTag.None;
        }

        #endregion

        #region UI 绑定

        public void BindUIComponent(GameObject uiInstance, List<Component> componentCache)
        {
            _uiInstance = uiInstance;
            if (_uiInstance == null) return;

            if (componentCache == null)
            {
                componentCache = new List<Component>();
                _uiInstance.GetComponentsInChildren(true, componentCache);
            }

            BindUIComponentWithCache(_uiInstance, componentCache);

            // 子 Entry 复用组件缓存
            foreach (var child in _children)
            {
                child.BindUIComponent(_uiInstance, componentCache);
            }
        }

        private void BindUIComponentWithCache(GameObject uiInstance, List<Component> componentCache)
        {
            // 遍历组件缓存，匹配 tag
            var locators = new List<XuiavrgLocator>();
            foreach (var comp in componentCache)
            {
                if (comp is XuiavrgLocator locator)
                {
                    locators.Add(locator);
                }

                if (comp is IXuiavrgFieldTagProvider tagProvider)
                {
                    // 匹配 Tag 和 TagIndex
                    if (tagProvider.EntryTag == this.Tag &&
                        (this.TagIndex == 0 || tagProvider.TagIndex == this.TagIndex))
                    {
                        // 找到匹配的 UI 组件，建立绑定
                        BuildComponentBinding(comp);
                    }
                }
            }

            // 如果没有 IXuiavrgFieldTagProvider，尝试通过 Locator 匹配
            if (locators.Count > 0)
            {
                BuildLocatorBindings(locators);
            }
        }

        /// <summary>
        /// 通过标签提供者建立组件绑定
        /// </summary>
        private void BuildComponentBinding(Component comp)
        {
            // 根据组件类型和标记类型进行数据同步
            switch (comp)
            {
                case UnityEngine.UI.Text uiText when Tag == XuiavrgFieldTag.Label || Tag == XuiavrgFieldTag.Output:
                    uiText.text = GetFirstBindingValue()?.ToString() ?? "";
                    break;
                case UnityEngine.UI.Button _ when Tag == XuiavrgFieldTag.Button:
                    // 按钮绑定在 BindUIEvents 中处理
                    break;
                case UnityEngine.UI.Toggle uiToggle when Tag == XuiavrgFieldTag.Toggle:
                    if (GetFirstBindingValue() is bool boolVal)
                        uiToggle.isOn = boolVal;
                    break;
                case UnityEngine.UI.Slider uiSlider when Tag == XuiavrgFieldTag.Slider:
                    if (GetFirstBindingValue() is float floatVal)
                        uiSlider.value = floatVal;
                    break;
                case UnityEngine.UI.InputField uiInput when Tag == XuiavrgFieldTag.Input:
                    uiInput.text = GetFirstBindingValue()?.ToString() ?? "";
                    break;
                case UnityEngine.UI.Dropdown uiDropdown when Tag == XuiavrgFieldTag.Dropdown:
                    if (GetFirstBindingValue() is int intVal)
                        uiDropdown.value = intVal;
                    break;
            }
        }

        /// <summary>
        /// 通过 Locator 建立绑定
        /// </summary>
        private void BuildLocatorBindings(List<XuiavrgLocator> locators)
        {
            foreach (var locator in locators)
            {
                foreach (var locProp in locator.Properties)
                {
                    // 按 propertyName 匹配成员名
                    if (_bindings.TryGetValue(locProp.propertyName, out var binding))
                    {
                        // 从数据端同步到 UI
                        var dataValue = binding.GetValue();
                        locProp.SetValue(dataValue);
                    }
                }
            }
        }

        /// <summary>
        /// 获取第一个绑定的值
        /// </summary>
        private object GetFirstBindingValue()
        {
            foreach (var kvp in _bindings)
            {
                var val = kvp.Value.GetValue();
                if (val != null) return val;
            }
            return null;
        }

        #endregion

        #region 事件绑定

        /// <summary>
        /// 绑定 UI 交互事件（双向绑定）
        /// </summary>
        public void BindUIEvents()
        {
            if (_uiInstance == null) return;

            var allComponents = new List<Component>();
            _uiInstance.GetComponentsInChildren(true, allComponents);

            foreach (var comp in allComponents)
            {
                if (comp is IXuiavrgFieldTagProvider tagProvider)
                {
                    if (tagProvider.EntryTag != this.Tag) continue;
                    BindComponentEvents(comp);
                }
            }

            // 子 Entry 事件绑定
            foreach (var child in _children)
            {
                child.BindUIEvents();
            }
        }

        private void BindComponentEvents(Component comp)
        {
            switch (comp)
            {
                case UnityEngine.UI.Button uiButton:
                    uiButton.onClick.AddListener(() =>
                    {
                        SetFirstBindingValue(true);
                    });
                    break;
                case UnityEngine.UI.Toggle uiToggle:
                    uiToggle.onValueChanged.AddListener(val =>
                    {
                        SetFirstBindingValue(val);
                    });
                    break;
                case UnityEngine.UI.Slider uiSlider:
                    uiSlider.onValueChanged.AddListener(val =>
                    {
                        SetFirstBindingValue(val);
                    });
                    break;
                case UnityEngine.UI.InputField uiInput:
                    uiInput.onEndEdit.AddListener(val =>
                    {
                        SetFirstBindingValue(val);
                    });
                    break;
                case UnityEngine.UI.Dropdown uiDropdown:
                    uiDropdown.onValueChanged.AddListener(val =>
                    {
                        SetFirstBindingValue(val);
                    });
                    break;
            }
        }

        private void SetFirstBindingValue(object value)
        {
            foreach (var kvp in _bindings)
            {
                kvp.Value.SetValue(value);
                break;
            }
        }

        #endregion

        #region 生命周期

        public void OnEntryCreate()
        {
            _entryName = null;
            _entryPath = null;
            _parent = null;
            _fieldOrder = 0;
            _prefabOverrideName = null;
        }

        public void OnEntryBind()
        {
            BindUIEvents();
        }

        public void OnEntryRelease()
        {
            _entryName = null;
            _entryPath = null;
            _parent = null;
            _uiInstance = null;
            _boundPrefabGroup = null;
            _prefabOverrideName = null;
            Tag = XuiavrgFieldTag.None;
            TagIndex = 0;
            _fieldOrder = 0;

            // 清理绑定引用
            _bindings.Clear();

            // 递归释放子 Entry
            foreach (var child in _children)
            {
                Release(child);
            }
            _children.Clear();
        }

        public void OnEntryDestroy()
        {
            _bindings.Clear();
            _children.Clear();
            _uiInstance = null;
        }

        #endregion
    }
}
