using System;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace XericUI.Core.Vessel
{
    [Serializable]
    public class ObjectVessel<T0, T1> : VesselBase
        where T0 : Object
        where T1 : Object
    {
        [SerializeField] private Object target0 = default(T0);
        [SerializeField] private Object target1 = default(T1);

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
        
        
        public ObjectVessel()
        {
            
        }
        
        public ObjectVessel(Object target0, Object target1)
        {
            this.target0 = target0;
            this.target1 = target1;
        }
    }
}