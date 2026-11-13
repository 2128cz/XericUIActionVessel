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

            // ScrollRect 的 Content 为空时 ContentTransform 回落到表自己的 rectTransform：
            // 这时清空清的是表自己的子物体（Viewport/Content/手摆的装饰），越重建越空。
            if (m_ScrollRect != null && m_ScrollRect.content == null && ReferenceEquals(t, rectTransform))
            {
                Debug.LogWarning("[XTable] ScrollRect 的 Content 为空，跳过清空（否则会清掉表自己的子物体）：" + name);
                return;
            }

#if UNITY_EDITOR
            // 编辑器模式下先重置 HideFlags 再销毁，防止 DontSave 对象拒绝销毁。
            // Play 下 Destroy 是延迟销毁、同帧 childCount 不会减少——原来这里用 while 会死循环（Awake 出不来）。
            if (Application.isPlaying)
            {
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var child = t.GetChild(i);
                    child.gameObject.hideFlags = HideFlags.None;
                    Destroy(child.gameObject);
                }
            }
            else
            {
                // 预制体自己的子物体（场景实例里的 prefab-owned、以及 Prefab 模式下资产自己的子节点）
                // 一律不碰：对它们 DestroyImmediate 会抛 InvalidOperationException；而以前只判了
                // IsPartOfPrefabInstance —— 在 Prefab 模式里该判定为 false，于是会把资产自己的子节点删掉，
                // 表现为“进预制体一个样、出场景一个样”。这里改成逐个判定（DontSave 的是我们自己生成的，照删），
                // 两种上下文行为一致。
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var child = t.GetChild(i);
                    bool ours = (child.hideFlags & HideFlags.DontSave) != 0;
                    if (!ours && (PrefabUtility.IsPartOfPrefabInstance(child.gameObject)
                                  || PrefabUtility.IsPartOfPrefabAsset(child.gameObject)))
                        continue;
                    child.gameObject.hideFlags = HideFlags.None;
                    DestroyImmediate(child.gameObject);
                }
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
