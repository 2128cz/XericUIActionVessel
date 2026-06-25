using System;
using UnityEngine;
using XericUI.Core.Vessel;
#if dUI_TextMeshPro
using TMPro;
#endif

namespace XericUI.Core.Text
{
    public static partial class XericUIExtendHelper
    {
        public static string DebugSelfName => "<TextMeshPro>";
        
        #region TMP_Text

        /// <summary>
        /// 获取文本容器中的字符串内容
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetStringContext<T>(this ObjectVessel<T> obj)
#if dUI_TextMeshPro
            where T : TMP_Text
            => obj != null ? obj.Target.text : string.Empty;
#else
            where T : UnityEngine.UI.Text
            => obj != null ? obj.Target.text : string.Empty;
#endif
        
        /// <summary>
        /// 获取文本容器中的字符串内容
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void SetStringContext<T>(this ObjectVessel<T> obj, string value)
#if dUI_TextMeshPro
            where T : TMP_Text
        {
            if (obj != null)
                obj.Target.text = value;
            else
                Debug.LogError($"正在向空{DebugSelfName}对象赋值");
        }
#else
            where T : UnityEngine.UI.Text
        {
            if (obj != null)
                obj.Target.text = value;
            else
                Debug.LogError($"正在向空{DebugSelfName}对象赋值");
        }
#endif
        #endregion

    }
}