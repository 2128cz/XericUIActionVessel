using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace XericUI.OverrideLayout
{
    public abstract class XericHorizontalOrVerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        /// <summary>
        /// 布局时忽略不可见的成员
        /// </summary>
        public bool layoutIgnoreInactive = false;


        #region 黑白名单

        /*
         * 黑名单对象无法参与布局
         * 仅白名单对象允许参与布局
         */

        public bool enableListScreening = true;
        public bool blackListMode = false;

        private HashSet<RectTransform> m_whiteList_HashSet;
        private HashSet<RectTransform> m_blackList_HashSet;

        [SerializeField] private List<RectTransform> m_whiteList_list;
        [SerializeField] private List<RectTransform> m_blackList_list;

        private bool m_whiteListDirty = true;
        private bool m_blackListDirty = true;

        public List<RectTransform> WhiteList
        {
            get => m_whiteList_list ??= ListPool<RectTransform>.Get();

            set
            {
                if (value == null)
                    return;
                m_whiteListDirty = true;
                if (m_whiteList_list != value)
                {
                    if (m_whiteList_list != null)
                        ListPool<RectTransform>.Release(m_whiteList_list);
                    m_whiteList_list = value;
                }
            }
        }

        public List<RectTransform> BlackList
        {
            get => m_blackList_list ??= ListPool<RectTransform>.Get();
            set
            {
                if (value == null)
                    return;
                m_blackListDirty = true;
                if (m_blackList_list != value)
                {
                    if (m_blackList_list != null)
                        ListPool<RectTransform>.Release(m_blackList_list);
                    m_blackList_list = value;
                }
            }
        }

        #endregion

#if UNITY_EDITOR

        protected override void Reset()
        {
            base.Reset();
            enableListScreening = true;
            blackListMode = false;
        }

        protected override void Update()
        {
            base.Update();
        }
#endif

        #region 黑白名单

        protected bool CanSkipLayout(RectTransform target)
        {
            if (!enableListScreening)
                return layoutIgnoreInactive && !target.gameObject.activeInHierarchy;
            return (blackListMode ? BlackListContains(target) : !WhiteListContains(target)) || 
                   (layoutIgnoreInactive && !target.gameObject.activeInHierarchy);
        }

        protected bool WhiteListContains(RectTransform target)
            => TackListContains(target, ref m_whiteListDirty, ref m_whiteList_list, ref m_whiteList_HashSet);

        protected bool BlackListContains(RectTransform target)
            => TackListContains(target, ref m_blackListDirty, ref m_blackList_list, ref m_blackList_HashSet);

        private bool TackListContains(RectTransform target, ref bool islistDirty, ref List<RectTransform> list,
            ref HashSet<RectTransform> hashlist)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return list.Contains(target);
#endif
            
            if (!islistDirty && list.Count == hashlist.Count)
                return hashlist.Contains(target);

            islistDirty = false;
            var hashset = HashSetPool<RectTransform>.Get();
            hashset.AddRange(list);
            if (hashlist != null)
                HashSetPool<RectTransform>.Release(hashlist);
            hashlist = hashset;
            return hashlist.Contains(target);
        }

        #endregion
    }
}