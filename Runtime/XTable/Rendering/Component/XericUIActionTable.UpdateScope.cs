using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        private sealed class UpdateContext
        {
            public TableDirtyType DirtyFlags;
            public bool HasStructuralChange;
            public bool InvalidatePrefabBindings;
            public bool RequiresImmediateRefresh;
            public readonly Dictionary<GameObject, Action<GameObject>> PrefabReleaseCallbacks = new();

            public void Reset()
            {
                DirtyFlags = TableDirtyType.None;
                HasStructuralChange = false;
                InvalidatePrefabBindings = false;
                RequiresImmediateRefresh = false;
                PrefabReleaseCallbacks.Clear();
            }

            public void MergeFrom(UpdateContext child)
            {
                DirtyFlags |= child.DirtyFlags;
                HasStructuralChange |= child.HasStructuralChange;
                InvalidatePrefabBindings |= child.InvalidatePrefabBindings;
                RequiresImmediateRefresh |= child.RequiresImmediateRefresh;
                foreach (var pair in child.PrefabReleaseCallbacks)
                    if (!PrefabReleaseCallbacks.ContainsKey(pair.Key))
                        PrefabReleaseCallbacks.Add(pair.Key, pair.Value);
            }
        }

        public sealed class UpdateScope : IDisposable
        {
            private XericUIActionTable m_Table;
            private readonly long m_Token;
            private bool m_Disposed;

            internal UpdateScope(XericUIActionTable table, long token)
            {
                m_Table = table;
                m_Token = token;
            }

            public void Dispose()
            {
                if (m_Disposed) return;
                m_Disposed = true;
                if (m_Table != null)
                    m_Table.EndCheckUpdate(m_Token);
                m_Table = null;
            }
        }

        [NonSerialized] private readonly Stack<UpdateContext> m_UpdateContextStack = new();
        [NonSerialized] private readonly Stack<UpdateContext> m_UpdateContextPool = new();
        [NonSerialized] private long m_NextUpdateToken;
        [NonSerialized] private readonly Stack<long> m_UpdateTokenStack = new();

        private bool IsCheckingUpdate => m_UpdateContextStack.Count > 0;

        /// <summary>
        /// 开始一个可嵌套的批量更新作用域。作用域内的数据修改立即生效，渲染与结构资源整理延迟到最外层 Dispose。
        /// </summary>
        public UpdateScope CheckUpdate()
        {
            var context = m_UpdateContextPool.Count > 0 ? m_UpdateContextPool.Pop() : new UpdateContext();
            context.Reset();
            m_UpdateContextStack.Push(context);
            long token = ++m_NextUpdateToken;
            m_UpdateTokenStack.Push(token);
            return new UpdateScope(this, token);
        }

        private void EndCheckUpdate(long token)
        {
            if (m_UpdateTokenStack.Count == 0 || m_UpdateTokenStack.Peek() != token)
            {
                Debug.LogError("[XericUIActionTable] CheckUpdate 作用域必须按 LIFO 顺序释放。", this);
                return;
            }

            m_UpdateTokenStack.Pop();
            UpdateContext context = m_UpdateContextStack.Pop();
            if (m_UpdateContextStack.Count > 0)
            {
                m_UpdateContextStack.Peek().MergeFrom(context);
                ReturnUpdateContext(context);
                return;
            }

            try
            {
                CommitUpdateContext(context);
            }
            finally
            {
                ReturnUpdateContext(context);
            }
        }

        private void ReturnUpdateContext(UpdateContext context)
        {
            context.Reset();
            m_UpdateContextPool.Push(context);
        }

        private void CommitUpdateContext(UpdateContext context)
        {
            if (context.DirtyFlags == TableDirtyType.None) return;

            if (context.HasStructuralChange)
                CommitStructuralRenderCleanup(context.InvalidatePrefabBindings, context.PrefabReleaseCallbacks);

            ApplyDirty(context.DirtyFlags);
            if (context.RequiresImmediateRefresh)
                RefreshView();
            RefreshSelectionOverlays();
        }

        private void RecordStructuralChange(bool invalidatePrefabBindings)
        {
            if (!IsCheckingUpdate) return;
            UpdateContext context = m_UpdateContextStack.Peek();
            context.HasStructuralChange = true;
            context.InvalidatePrefabBindings |= invalidatePrefabBindings;
        }

        private void CaptureActivePrefabReleaseCallbacks()
        {
            if (!IsCheckingUpdate || m_ActivePrefabMap == null) return;
            var callbacks = m_UpdateContextStack.Peek().PrefabReleaseCallbacks;
            foreach (var pair in m_ActivePrefabMap)
            {
                var callback = TableData.GetCell(pair.Key.Item1, pair.Key.Item2)?.OnPrefabRelease;
                if (callback != null && !callbacks.ContainsKey(pair.Value))
                    callbacks.Add(pair.Value, callback);
            }
        }

        private void RequestRefresh(TableDirtyType type, bool immediate)
        {
            MarkDirty(type);
            if (IsCheckingUpdate)
            {
                m_UpdateContextStack.Peek().RequiresImmediateRefresh |= immediate;
                return;
            }
            if (immediate)
                RefreshView();
        }

        private void ThrowIfCheckingUpdate(string operation)
        {
            if (IsCheckingUpdate)
                throw new InvalidOperationException($"{operation} 不能在 CheckUpdate 作用域内调用。");
        }
    }
}
