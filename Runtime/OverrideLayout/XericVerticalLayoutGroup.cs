using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;

namespace XericUI.OverrideLayout
{
    [AddComponentMenu("Xeric UI Vessel/Layout/Xeric Vertical Layout Group", 10)]
    /// <summary>
    /// Layout child layout elements below each other.
    /// </summary>
    public class XericVerticalLayoutGroup : XericHorizontalOrVerticalLayoutGroup
    {
        protected XericVerticalLayoutGroup()
        {}

        public override void CalculateLayoutInputHorizontal()
        {
            rectChildren.Clear();
            var toIgnoreList = ListPool<Component>.Get();
            for (int i = 0; i < rectTransform.childCount; i++)
            {
                var rect = rectTransform.GetChild(i) as RectTransform;
                if (rect == null || CanSkipLayout(rect))
                    continue;

                rect.GetComponents(typeof(ILayoutIgnorer), toIgnoreList);

                if (toIgnoreList.Count == 0)
                {
                    rectChildren.Add(rect);
                    continue;
                }

                for (int j = 0; j < toIgnoreList.Count; j++)
                {
                    var ignorer = (ILayoutIgnorer)toIgnoreList[j];
                    if (!ignorer.ignoreLayout)
                    {
                        rectChildren.Add(rect);
                        break;
                    }
                }
            }

            ListPool<Component>.Release(toIgnoreList);
            m_Tracker.Clear();
            CalcAlongAxis(0, true);
        }

        public override void CalculateLayoutInputVertical()
        {
            CalcAlongAxis(1, true);
        }

        public override void SetLayoutHorizontal()
        {
            SetChildrenAlongAxis(0, true);
        }

        public override void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1, true);
        }
    }
}