using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XericUI.Core.Vessel
{
    /// <summary>
    /// 组件界面泛型容器
    /// 详情参阅Xeric Library本体库Wiki中关于组件容器的说明。
    /// </summary>
    /// <typeparam name="T0"></typeparam>
    [Serializable]
    public sealed class ObjectVessel<T0> : VesselBase
        where T0 : Object
    {
        [SerializeField]
        private Object target = default(T0);

        /// <summary>
        /// 获取此包裹器中的
        /// </summary>
        public T0 Target
        {
            get => target as T0;
            set => target = value;
        }

        public ObjectVessel()
        {
            
        }
        
        public ObjectVessel(Object target)
        {
            this.target = target;
        }

        public static implicit operator T0(ObjectVessel<T0> value)
        {
            return (T0)value.target;
        }
    }
}