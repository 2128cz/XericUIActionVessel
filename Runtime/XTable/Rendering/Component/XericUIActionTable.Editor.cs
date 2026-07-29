using UnityEngine;
using XericUI.Core.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        #region 编辑器模式支持

        [System.NonSerialized] private bool m_EditorUpdateSubscribed;

        private void SubscribeEditorUpdate()
        {
            if (m_EditorUpdateSubscribed) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorUpdate;
            m_EditorUpdateSubscribed = true;
#endif
        }

        private void UnsubscribeEditorUpdate()
        {
            if (!m_EditorUpdateSubscribed) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
            m_EditorUpdateSubscribed = false;
#endif
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null || !isActiveAndEnabled)
            {
                UnsubscribeEditorUpdate();
                return;
            }

            if (m_IsRebuilding || IsCheckingUpdate || !m_DirtyFlag.IsDirty) return;

            var flags = m_DirtyFlag.Flags;
            if ((flags & ~(TableDirtyType.ScrollChanged | TableDirtyType.SelectionChanged)) == 0)
                RefreshViewLight();
            else
                RefreshView();
        }
#endif

        #endregion

        /// <summary>销毁 transform 下所有子对象（用于域重载后清理残留）</summary>
        private void ClearAllChildren()
        {
            Transform t = ContentTransform;
            if (t == null) return;

#if UNITY_EDITOR
            // 编辑器模式下先重置 HideFlags 再销毁，防止 DontSave 对象拒绝销毁
            while (t.childCount > 0)
            {
                var child = t.GetChild(0);
                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
#else
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                Destroy(child.gameObject);
            }
#endif
        }

        /// <summary>设置所有动态子对象的可见性（仅编辑器）</summary>
        public void SetChildrenVisible(bool visible)
        {
            m_ChildrenVisible = visible;
#if UNITY_EDITOR
            Transform t = ContentTransform;
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                child.gameObject.hideFlags = visible
                    ? HideFlags.DontSave
                    : HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
#endif
        }
    }
}
