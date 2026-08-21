using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表工具管理器。
    ///
    /// 负责管理所有图表渲染工具的注册、生命周期编排、执行顺序控制。
    /// 挂在 XChartBase 上，作为工具的中央调度器。
    /// </summary>
    public class XChartToolsManager
    {
        // ── 工具列表 ──────────────────────────────────────────────

        private List<XChartToolBase> m_Tools = new List<XChartToolBase>(8);

        /// <summary>只读工具列表（用于外部遍历）</summary>
        public IReadOnlyList<XChartToolBase> Tools => m_Tools;

        /// <summary>当前工具数量</summary>
        public int ToolCount => m_Tools.Count;

        // ── 管理节点 ──────────────────────────────────────────────

        /// <summary>所有工具的根父节点（由 XChartBase 指定）</summary>
        public Transform ToolsRoot { get; set; }

        // ── 版本控制（防溢出） ────────────────────────────────────

        private int m_Version;

        // ── 添加工具 ──────────────────────────────────────────────

        /// <summary>
        /// 添加一个图表工具。
        /// 自动在 ToolsRoot 下创建同名子 GameObject，挂载并初始化工具。
        /// </summary>
        public T AddTool<T>(string toolName, int orderIndex = 0)
            where T : XChartToolBase, new()
        {
            if (ToolsRoot == null)
                throw new InvalidOperationException("ToolsRoot 未设置，无法添加工具");

            var flags = Application.isPlaying
                ? HideFlags.HideAndDontSave
                : HideFlags.DontSaveInEditor;

            var rootObject = new GameObject(toolName);
            rootObject.transform.SetParent(ToolsRoot, false);
            rootObject.hideFlags = flags;

            var tool = new T();
            tool.InternalInitialize(this, toolName, rootObject, orderIndex);

            // 按 OrderIndex 插入到有序位置
            int insertIdx = m_Tools.Count;
            for (int i = 0; i < m_Tools.Count; i++)
            {
                if (m_Tools[i].OrderIndex > orderIndex)
                {
                    insertIdx = i;
                    break;
                }
            }
            m_Tools.Insert(insertIdx, tool);

            rootObject.transform.SetSiblingIndex(insertIdx);
            m_Version++;
            return tool;
        }

        // ── 移除工具 ──────────────────────────────────────────────

        public void RemoveTool(XChartToolBase tool)
        {
            if (tool == null || !m_Tools.Contains(tool)) return;
            tool.Dispose();
            m_Tools.Remove(tool);
            m_Version++;
        }

        public bool RemoveToolByName(string toolName)
        {
            for (int i = m_Tools.Count - 1; i >= 0; i--)
            {
                if (m_Tools[i].ToolName == toolName)
                {
                    RemoveTool(m_Tools[i]);
                    return true;
                }
            }
            return false;
        }

        public void RemoveAllTools()
        {
            for (int i = m_Tools.Count - 1; i >= 0; i--)
                m_Tools[i].Dispose();
            m_Tools.Clear();
            m_Version++;
        }

        // ── 查找工具 ──────────────────────────────────────────────

        public T FindTool<T>(string toolName = null) where T : XChartToolBase
        {
            for (int i = 0; i < m_Tools.Count; i++)
            {
                if (m_Tools[i] is T tool)
                {
                    if (toolName == null || tool.ToolName == toolName)
                        return tool;
                }
            }
            return null;
        }

        // ── 生命周期驱动 ──────────────────────────────────────────

        public void UpdateAll()
        {
            for (int i = 0; i < m_Tools.Count; i++)
            {
                var tool = m_Tools[i];
                if (!tool.IsEnabled || !tool.IsInitialized) continue;

                int snap = m_Version;
                tool.OnUpdate();
                if (snap != m_Version && (i >= m_Tools.Count || m_Tools[i] != tool))
                    i--;
            }
        }

        public void RenderAll()
        {
            for (int i = 0; i < m_Tools.Count; i++)
            {
                var tool = m_Tools[i];
                if (!tool.IsEnabled || !tool.IsInitialized) continue;

                int snap = m_Version;
                tool.OnRender();
                if (snap != m_Version && (i >= m_Tools.Count || m_Tools[i] != tool))
                    i--;
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        public void Dispose()
        {
            RemoveAllTools();
            m_Tools.Clear();
        }
    }
}