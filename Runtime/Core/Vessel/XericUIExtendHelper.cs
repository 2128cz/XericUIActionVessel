using UnityEngine;

namespace XericUI.Core.Vessel
{
    public static partial class XericUIExtendHelper
    {
        /// <summary>
        /// 检查容器有效
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T0"></typeparam>
        /// <returns></returns>
        public static bool IsValid<T0>(this ObjectVessel<T0> obj)
            where T0 : Object
            => obj != null && obj.Target != null;
        /// <summary>
        /// 检查容器有效
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T0"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        public static bool IsValid<T0, T1>(this ObjectVessel<T0, T1> obj)
            where T0 : Object
            where T1 : Object
            => obj != null && obj.Target0 != null && obj.Target1 != null;
        /// <summary>
        /// 检查容器有效
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T0"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static bool IsValid<T0, T1, T2>(this ObjectVessel<T0, T1, T2> obj)
            where T0 : Object
            where T1 : Object
            where T2 : Object
            => obj != null && obj.Target0 != null && obj.Target1 != null && obj.Target2 != null;
    }
}