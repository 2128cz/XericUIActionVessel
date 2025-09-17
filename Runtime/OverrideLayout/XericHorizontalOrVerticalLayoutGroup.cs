using Sirenix.OdinInspector;
using UnityEngine.UI;

namespace XericUI.OverrideLayout
{
    public abstract class XericHorizontalOrVerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        /// <summary>
        /// 布局时忽略不可见的成员
        /// </summary>
        public bool layoutIgnoreInactive = false;

#if UNITY_EDITOR
        protected override void Update()
        {
            base.Update();
        }
#endif
    }
}