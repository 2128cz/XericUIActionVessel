using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using XericLibrary.Runtime.MacroLibrary;

namespace XericUI.Helper
{
    /// <summary>
    /// UI输入系统帮助
    /// </summary>
    public static class EventSystemHelper
    {
        #region 字段属性

        private static int raycastResultsFrameIndex = -1;
        public static List<RaycastResult> raycastResults = new List<RaycastResult>();

        #endregion

        #region 辅助功能

        /// <summary>
        /// 根据重载可选覆盖返回的事件系统
        /// </summary>
        /// <param name="overrideEventSystem"></param>
        /// <returns></returns>
        private static EventSystem GetEventSystem(EventSystem overrideEventSystem = null)
            => overrideEventSystem == null
                ? EventSystem.current
                : overrideEventSystem;

        /// <summary>
        /// 当前是否步进帧
        /// </summary>
        /// <param name="frameIndex"></param>
        /// <param name="holdon">保持状态</param>
        /// <returns></returns>
        private static bool IsFrameIndexChange(ref int frameIndex, bool holdon = false)
        {
            if (frameIndex == Time.frameCount)
                return false;
            if (holdon)
                return true;
            frameIndex = Time.frameCount;
            return true;
        }

        #endregion

        #region 光标在其上

        /// <summary>
        /// 判断当前光标是否处于任意UI对象上
        /// </summary>
        /// <param name="overrideEventSystem"></param>
        /// <returns></returns>
        public static bool IsPointerOverGameObject(EventSystem overrideEventSystem = null)
        {
            overrideEventSystem = GetEventSystem(overrideEventSystem);
            return overrideEventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// 自动更新射线监测
        /// </summary>
        /// <param name="overrideEventSystem"></param>
        /// <returns></returns>
        public static List<RaycastResult> GetRaycastAllResults(EventSystem overrideEventSystem = null)
        {
            if (!IsFrameIndexChange(ref raycastResultsFrameIndex))
                return raycastResults;

            overrideEventSystem = GetEventSystem(overrideEventSystem);
            var pointerData = new PointerEventData(overrideEventSystem)
            {
                position = Input.mousePosition
            };
            overrideEventSystem.RaycastAll(pointerData, raycastResults);
            return raycastResults;
        }

        /// <summary>
        /// 判断当前光标是否处于特定组件上
        /// </summary>
        /// <param name="component">返回找到的组件</param>
        /// <param name="compatibility">是否启用兼容性，当在所有击中的元素上找不到目标时，继续向上查找</param>
        /// <param name="overrideEventSystem"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool IsPointerOverUIWithComponent<T>(out T component, bool compatibility = true, EventSystem overrideEventSystem = null)
            where T : Component
        {
            var results = GetRaycastAllResults(overrideEventSystem);
            if (results.Count <= 0)
            {
                component = null;
                return false;
            }
            component = results
                .Select(a => a.gameObject.GetComponent<T>())
                .FirstOrDefault(a => a != null);
            if (compatibility && component == null)
                component = results[^1].gameObject.GetComponentInParent<T>();
            return component != null;
        }
        
        #endregion
    }
}