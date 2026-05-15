using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
// using Action = Unity.Plastic.Antlr3.Runtime.Misc.Action;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace XericUI.Helper
{
    public static class EditorOverrideUIHelper
    {
        /// <summary>
        /// 替换菜单组件上下文
        /// </summary>
        /// <remarks>
        /// 在组件上使用如下的方法定义替换
        /// 
        /// [MenuItem("CONTEXT/XXX/ReplaceToXXX", true)]
        /// private static bool ValidateReplaceWithCustomScrollRect(MenuCommand command) 
        ///     => command.context is ScrollRect;
        ///     
        /// [MenuItem("CONTEXT/XXX/ReplaceToXXX", false, 10)]
        /// private static void ReplaceWithCustomScrollRect(MenuCommand command) 
        ///     => command.ReplaceCommandContext&lt;SourceXXX, TargetXXX&gt;();
        ///
        /// 不过实测unity的json序列化也无法处理引用，还是需要手动将引用类型传过来
        /// </remarks>
        /// <param name="command"></param>
#if UNITY_EDITOR
        public static TTarget ReplaceCommandContext<TSource, TTarget>(this MenuCommand command)
            where TSource : Component
            where TTarget : Component
        {
            TSource original = command.context as TSource;
            if (original == null) return null;
            GameObject go = original.gameObject;

            // 标记撤销
            Undo.SetCurrentGroupName($"替换{typeof(TSource).Name}为{typeof(TTarget).Name}");
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(go, "替换组件");
            
            // 主要的替换流程，不过unity好像也只能处理非引用对象，废物Unity
            string jsonData = EditorJsonUtility.ToJson(original);
            Object.DestroyImmediate(original);
            TTarget newComponent = go.AddComponent<TTarget>();
            EditorJsonUtility.FromJsonOverwrite(jsonData, newComponent);

            EditorUtility.SetDirty(go);
            
            // 结束撤销标记
            Undo.RegisterCreatedObjectUndo(newComponent, "替换组件");
            Undo.CollapseUndoOperations(undoGroup);
            
            Debug.Log("组件替换完成");
            return newComponent;
        }
#else
        private static void ReplaceCommandContext<T1, T2>(this Object command) 
            => throw new Exception("不能在运行时使用XericUIActionVessel编辑器功能");
#endif
        
        
#if UNITY_EDITOR
        public static void ReplaceCommandContext<TSource, TTarget, T>(
            this MenuCommand command, Func<TSource, T> referenceExtractor, Action<TTarget, T> referenceApplier)
            where TSource : Component
            where TTarget : Component
        {
            TSource original = command.context as TSource;
            var references = referenceExtractor(original);
            
            TTarget component = command.ReplaceCommandContext<TSource, TTarget>();

            referenceApplier(component, references);
        }
#else
        private static void ReplaceCommandContext<T1, T2, T3>(this Object command, 
            Func<T1, T3> referenceExtractor, Action<T2, T3> referenceApplier) 
            => throw new Exception("不能在运行时使用XericUIActionVessel编辑器功能");
#endif
    }
}