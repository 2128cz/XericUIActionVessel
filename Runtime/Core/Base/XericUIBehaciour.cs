using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XericUI.Core.Base
{
    public class XericUIBehaciour : UIBehaviour
    {
        [InfoBox("脚本属性存在空引用！请检查开放属性后再试", InfoMessageType.Error, VisibleIf = "PublicFieldNullReference")]
        [HideLabel, DisplayAsString]
        string __EMPTYFIELD;

        
        
        
        protected bool PublicFieldNullReference;
        
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
        
    }
}