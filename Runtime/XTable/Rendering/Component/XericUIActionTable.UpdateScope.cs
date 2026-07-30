using System;
using System.Collections.Generic;
using UnityEngine;
using XericUI.XTable.Core;

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        private enum UpdateCommandType
        {
            SetFreezeCount,
            SetCellData,
            SetCellText,
            SetCellImage,
            SetCellPrefab,
            ClearCellText,
            ClearCellImage,
            ClearCellPrefab,
            SetCellStyle,
            SetCellStyleNamespace,
            ClearCellStyle,
            MergeCells,
            UnmergeCells,
            AddRow,
            AddColumn,
            RemoveRow,
            RemoveColumn,
            SetRowHeight,
            SetColWidth,
            ForceStyleRefresh
        }

        private readonly struct UpdateCommand
        {
            public readonly UpdateCommandType Type;
            public readonly int Row;
            public readonly int Col;
            public readonly int IntValue1;
            public readonly int IntValue2;
            public readonly float FloatValue;
            public readonly string StringValue1;
            public readonly string StringValue2;
            public readonly XTableCellData CellData;
            public readonly Texture2D Texture;
            public readonly GameObject Prefab;

            public UpdateCommand(UpdateCommandType type, int row = 0, int col = 0,
                int intValue1 = 0, int intValue2 = 0, float floatValue = 0f,
                string stringValue1 = null, string stringValue2 = null,
                XTableCellData cellData = null, Texture2D texture = null, GameObject prefab = null)
            {
                Type = type;
                Row = row;
                Col = col;
                IntValue1 = intValue1;
                IntValue2 = intValue2;
                FloatValue = floatValue;
                StringValue1 = stringValue1;
                StringValue2 = stringValue2;
                CellData = cellData;
                Texture = texture;
                Prefab = prefab;
            }
        }

        private sealed class UpdateContext
        {
            public TableDirtyType DirtyFlags;
            public readonly List<UpdateCommand> Commands = new();

            public void Reset()
            {
                DirtyFlags = TableDirtyType.None;
                Commands.Clear();
            }

            public void MergeFrom(UpdateContext child)
            {
                DirtyFlags |= child.DirtyFlags;
                Commands.AddRange(child.Commands);
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
                m_Table?.EndCheckUpdate(m_Token);
                m_Table = null;
            }
        }

        [NonSerialized] private readonly Stack<UpdateContext> m_UpdateContextStack = new();
        [NonSerialized] private readonly Stack<UpdateContext> m_UpdateContextPool = new();
        [NonSerialized] private readonly Stack<long> m_UpdateTokenStack = new();
        [NonSerialized] private long m_NextUpdateToken;

        private bool IsCheckingUpdate => m_UpdateContextStack.Count > 0;

        /// <summary>开始一个可嵌套的命令缓冲作用域。可变 API 在最外层 Dispose 时按调用顺序执行。</summary>
        public UpdateScope CheckUpdate()
        {
            UpdateContext context = m_UpdateContextPool.Count > 0 ? m_UpdateContextPool.Pop() : new UpdateContext();
            context.Reset();
            m_UpdateContextStack.Push(context);
            long token = ++m_NextUpdateToken;
            m_UpdateTokenStack.Push(token);
            return new UpdateScope(this, token);
        }

        private bool TryEnqueueUpdateCommand(UpdateCommand command)
        {
            if (!IsCheckingUpdate) return false;
            m_UpdateContextStack.Peek().Commands.Add(command);
            return true;
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

            m_UpdateContextStack.Push(context);
            try
            {
                Dictionary<GameObject, Action<GameObject>> prefabCallbacks = CaptureActivePrefabReleaseCallbacks();
                ExecuteUpdateCommands(context);
                if (context.DirtyFlags == TableDirtyType.None) return;

                m_UpdateContextStack.Pop();
                ReleaseRenderedBindings(prefabCallbacks);
                ApplyDirty(context.DirtyFlags);
                RefreshView(false);
                RefreshSelectionOverlays();
            }
            finally
            {
                if (m_UpdateContextStack.Count > 0 && ReferenceEquals(m_UpdateContextStack.Peek(), context))
                    m_UpdateContextStack.Pop();
                ReturnUpdateContext(context);
            }
        }

        private void ExecuteUpdateCommands(UpdateContext context)
        {
            foreach (UpdateCommand command in context.Commands)
            {
                switch (command.Type)
                {
                    case UpdateCommandType.SetFreezeCount: SetFreezeCountImmediate(command.Row, command.Col, false); break;
                    case UpdateCommandType.SetCellData: SetCellDataImmediate(command.Row, command.Col, command.CellData); break;
                    case UpdateCommandType.SetCellText: SetCellTextImmediate(command.Row, command.Col, command.StringValue1); break;
                    case UpdateCommandType.SetCellImage: SetCellImageImmediate(command.Row, command.Col, command.Texture); break;
                    case UpdateCommandType.SetCellPrefab: SetCellPrefabImmediate(command.Row, command.Col, command.Prefab); break;
                    case UpdateCommandType.ClearCellText: ClearCellTextImmediate(command.Row, command.Col); break;
                    case UpdateCommandType.ClearCellImage: ClearCellImageImmediate(command.Row, command.Col); break;
                    case UpdateCommandType.ClearCellPrefab: ClearCellPrefabImmediate(command.Row, command.Col); break;
                    case UpdateCommandType.SetCellStyle: SetCellStyleImmediate(command.Row, command.Col, command.StringValue1, command.StringValue2); break;
                    case UpdateCommandType.SetCellStyleNamespace: SetCellStyleNamespaceImmediate(command.Row, command.Col, command.StringValue1); break;
                    case UpdateCommandType.ClearCellStyle: ClearCellStyleImmediate(command.Row, command.Col); break;
                    case UpdateCommandType.MergeCells: MergeCellsImmediate(command.Row, command.Col, command.IntValue1, command.IntValue2); break;
                    case UpdateCommandType.UnmergeCells: UnmergeCellsImmediate(command.Row, command.Col); break;
                    case UpdateCommandType.AddRow: AddRowImmediate(command.FloatValue); break;
                    case UpdateCommandType.AddColumn: AddColumnImmediate(command.FloatValue); break;
                    case UpdateCommandType.RemoveRow: RemoveRowImmediate(command.Row); break;
                    case UpdateCommandType.RemoveColumn: RemoveColumnImmediate(command.Col); break;
                    case UpdateCommandType.SetRowHeight: SetRowHeightImmediate(command.Row, command.FloatValue); break;
                    case UpdateCommandType.SetColWidth: SetColWidthImmediate(command.Col, command.FloatValue); break;
                    case UpdateCommandType.ForceStyleRefresh: ForceStyleRefreshImmediate(); break;
                }
            }
        }

        private void ReturnUpdateContext(UpdateContext context)
        {
            context.Reset();
            m_UpdateContextPool.Push(context);
        }

        private Dictionary<GameObject, Action<GameObject>> CaptureActivePrefabReleaseCallbacks()
        {
            var callbacks = new Dictionary<GameObject, Action<GameObject>>();
            if (m_ActivePrefabMap == null) return callbacks;
            foreach (var pair in m_ActivePrefabMap)
            {
                Action<GameObject> callback = TableData.GetCell(pair.Key.Item1, pair.Key.Item2)?.OnPrefabRelease;
                if (callback != null && !callbacks.ContainsKey(pair.Value))
                    callbacks.Add(pair.Value, callback);
            }
            return callbacks;
        }

        private void RequestRefresh(TableDirtyType type, bool immediate)
        {
            MarkDirty(type);
            if (IsCheckingUpdate) return;
            if (immediate) RefreshView();
        }

        private void ThrowIfCheckingUpdate(string operation)
        {
            if (IsCheckingUpdate)
                throw new InvalidOperationException($"{operation} 不能在 CheckUpdate 作用域内调用。");
        }
    }
}
