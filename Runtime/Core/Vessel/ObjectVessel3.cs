using System;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace XericUI.Core.Vessel
{
    [Serializable]
    public sealed class ObjectVessel<T0, T1, T2> : VesselBase
        where T0 : Object
        where T1 : Object
        where T2 : Object
    {
        [SerializeField] private Object target0 = default(T0);
        [SerializeField] private Object target1 = default(T1);
        [SerializeField] private Object target2 = default(T2);

        public T0 Target0
        {
            get => target0 as T0;
            set => target0 = value;
        }

        public T1 Target1
        {
            get => target1 as T1;
            set => target1 = value;
        }

        public T2 Target2
        {
            get => target2 as T2;
            set => target2 = value;
        }


        public ObjectVessel()
        {
        }

        public ObjectVessel(Object target0, Object target1, Object target2)
        {
            this.target0 = target0;
            this.target1 = target1;
            this.target2 = target2;
        }

        public static implicit operator T0(ObjectVessel<T0, T1, T2> value)
        {
            return (T0)value.target0;
        }
        
        public static implicit operator T1(ObjectVessel<T0, T1, T2> value)
        {
            return (T1)value.target1;
        }
        
        public static implicit operator T2(ObjectVessel<T0, T1, T2> value)
        {
            return (T2)value.target2;
        }
    }
}