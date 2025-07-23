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
            where T : TMP_Text
            => obj != null ? obj.Target.text : string.Empty;

        /// <summary>
        /// 获取文本容器中的字符串内容
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void SetStringContext<T>(this ObjectVessel<T> obj, string value)
            where T : TMP_Text
        {
            if (obj != null)
                obj.Target.text = value;
            else
                Debug.LogError($"正在向空{DebugSelfName}对象赋值");
        }
        
        #endregion

    }
}