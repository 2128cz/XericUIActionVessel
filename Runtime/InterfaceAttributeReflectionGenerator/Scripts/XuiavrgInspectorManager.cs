using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deconstruction.Type.Peculiarity;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Pool;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 节点下生成反射属性管理器
    /// </summary>
    /// <remarks>
    /// 管理器需要挂载在unity节点中，管理器中指定的属性承载节点将用于生成属性内容。
    ///
    /// 为了准确生成界面属性，首先需要准备界面属性预制体。
    /// 在包含界面交互元素的预制体跟目录上挂载 <see cref="XuiavrgLocator"/> 脚本，其将作为组件入口，管理所有的交互组件，必须在脚本中添加所有允许交互的元素。
    /// （注：如果你没有odin插件，那么组件样式可能会与设计的不一致，请务必确保每个数组中只包含一个组件）
    /// 
    /// 然后在工程中创建 <see cref="XuiavrPropertyScriptableObject"/> 对象，指定对应的数据类型，将刚刚创建的界面预制体拖入，这样就完成了两者的绑定。
    /// 为了方便管理，再创建一个 <see cref="XuiavrgPropertyGroupScriptableObject"/> 对象，它可以成为一个组，快速管理和切换所需的数据集合。
    /// 
    /// 接下来，在管理器中填入使用的 <see cref="XuiavrgPropertyGroupScriptableObject"/> 对象，然后调用 SchemaHydration 方法传入负载，界面顷刻生成。
    ///
    /// </remarks>
    public class XuiavrgInspectorManager : MonoBehaviour
    {
        public static Func<XuiavrgInspectorProperty> CreatInspectorProperty = () => new XuiavrgInspectorProperty();
        public static Action<XuiavrgInspectorProperty> GetInspectorProperty = a => {};
        public static Action<XuiavrgInspectorProperty> ReleaseInspectorProperty = a => a.Clear();
        public static Action<XuiavrgInspectorProperty> DestoryInspectorProperty = a => {};
        public static ObjectPool<XuiavrgInspectorProperty> InspectorPropertyPool 
            = new ObjectPool<XuiavrgInspectorProperty>(CreatInspectorProperty, GetInspectorProperty, ReleaseInspectorProperty, DestoryInspectorProperty);

#if ODIN_INSPECTOR
        [LabelText("预制体组")]
#endif
        public XuiavrgPropertyGroupScriptableObject propertyUIPrefabsGroup;
#if ODIN_INSPECTOR
        [LabelText("反射属性预览")]
#endif
        public List<XuiavrgInspectorProperty> allProperty = new List<XuiavrgInspectorProperty>();
        
        
        /// <summary>
        /// 装填数据负载
        /// </summary>
        /// <param name="payload">数据端对象</param>
#if ODIN_INSPECTOR
        [Button]
#endif
        public void SchemaHydration(object payload)
        {
            // 清空环境和界面
            allProperty.Clear();
            InspectorPropertyPool.Clear();

            if (!propertyUIPrefabsGroup)
            {
                Debug.LogError("未指定编组，无法生成界面");
                return;
            }

            if (payload != null)
            {
                Debug.LogError("未指定有效负载，无法生成界面");
                return;
            }
            
            // 从反射构建
            var type = payload.GetType();
            SchemaHydration(allProperty, type, payload);
        }

        
        
        /// <summary>
        /// 状态数据负载
        /// </summary>
        /// <param name="properties">反射属性结合</param>
        /// <param name="payload">负载实例</param>
        /// <typeparam name="T">进行检查的负载类型</typeparam>
        private static void SchemaHydration<T>(List<XuiavrgInspectorProperty> properties, object payload)
            => SchemaHydration(properties, typeof(T), payload);
        
        /// <summary>
        /// 装填数据负载
        /// </summary>
        /// <param name="properties">反射属性集合</param>
        /// <param name="payloadType">进行检查的负载类型</param>
        /// <param name="payload">负载实例</param>
        private static void SchemaHydration(List<XuiavrgInspectorProperty> properties, Type payloadType, object payload)
        {
            bool needSort = false;
            foreach (var memberInfo in payloadType.GetRuntimeMemberInfo())
            {
                var mw = InspectorPropertyPool.Get();
                // 绑定负载
                mw.RegsiterDataValue(memberInfo, payload);
                // 字段属性上的所有特性都进行标记
                foreach (var memberAttribute in memberInfo.GetCustomAttributes()
                             .OfType<IXuiavrgAttribute>())
                {
                    memberAttribute.BindProperty(mw);
                    needSort |= mw.inspectorOrder != 0; // 如果有成员使用了order
                }
                properties.Add(mw);
            }
            // 排序
            if (needSort)
                properties.Sort((property1, property2) => property1.inspectorOrder.CompareTo(property2.inspectorOrder));
        }
        
    }
}