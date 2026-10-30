using UnityEngine;
using XericUI.Core;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 虚拟窗体基类，提供基础的窗体标记功能
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.AnchorWindow + "/XvFormBase")]
    public class XvFormBase : MonoBehaviour
    {
        [SerializeField]
        private string m_FormID;

        /// <summary>
        /// 窗体唯一标识
        /// </summary>
        public string FormID
        {
            get => m_FormID;
            set => m_FormID = value;
        }

        /// <summary>
        /// 窗体是否处于活动状态
        /// </summary>
        public bool IsFormActive => gameObject.activeInHierarchy && enabled;

        protected virtual void Awake()
        {
            TryGenerateFormID();
        }

        /// <summary>
        /// 尝试自动生成FormID（如果当前为空）
        /// </summary>
        private void TryGenerateFormID()
        {
            if (string.IsNullOrEmpty(m_FormID))
            {
                m_FormID = ((uint)GetInstanceID()).ToString();
            }
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            TryGenerateFormID();
        }
#endif
    }
}
