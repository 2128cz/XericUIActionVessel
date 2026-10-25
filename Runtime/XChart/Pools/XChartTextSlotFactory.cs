using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XericUI.XChart.Pools
{
    /// <summary>
    /// 文本槽位工厂：运行时里<b>唯一</b>造 TMP 标签的地方（规范 §5）。
    ///
    /// <para>空闲的槽位停在池根（chart 根下的 <c>GeneratedTextPool</c> 节点）上并停用；
    /// 发落出去时挂到该 owner 自己的宿主节点。"父节点必须等于宿主"是有效性判据的一部分——
    /// 域重载后宿主是新实例，旧标签还挂在旧父节点上，这种必须丢弃重补，
    /// 不能让它带着上一轮的父子关系继续画。</para>
    ///
    /// <para>池根整块没了（chart 被销毁又重建）不抛异常：<see cref="EnsurePoolRoot"/> 直接重建。</para>
    /// </summary>
    public sealed class XChartTextSlotFactory : IXChartSlotFactory<TMP_Text>
    {
        /// <summary>池根节点名。chart 的回收校验和调试报表都按这个名字找它，所以只此一份。</summary>
        public const string PoolRootName = "GeneratedTextPool";

        private readonly RectTransform m_ChartRoot;
        private readonly Stack<TMP_Text> m_Free = new Stack<TMP_Text>();
        private RectTransform m_PoolRoot;
        private int m_SlotIndex;

        public XChartTextSlotFactory(RectTransform chartRoot)
        {
            m_ChartRoot = chartRoot;
        }

        /// <summary>池里闲置的槽位数（调试判据用：调度收敛后应当等于"曾经发落过又还回来的"）</summary>
        public int FreeCount => m_Free.Count;

        public TMP_Text Acquire()
        {
            var poolRoot = EnsurePoolRoot();
            if (poolRoot == null) return null;

            while (m_Free.Count > 0)
            {
                TMP_Text slot = m_Free.Pop();
                // 空闲表里也可能有域重载留下的僵尸件：验不过就当场销毁，别发出去
                if (!IsAlive(slot)) { DestroySlot(slot); continue; }
                slot.gameObject.SetActive(true);
                return slot;
            }

            var go = new GameObject("TextSlot_" + m_SlotIndex, typeof(RectTransform));
            m_SlotIndex++;
            go.transform.SetParent(poolRoot, false);
            go.hideFlags = XChartBase.CurrentFlags;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.enableWordWrapping = false;
            tmp.alignment = TextAlignmentOptions.Center;
            var rect = tmp.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return tmp;
        }

        public void Release(TMP_Text slot)
        {
            if (!IsAlive(slot)) return;
            slot.text = string.Empty;
            slot.gameObject.SetActive(false);
            var poolRoot = EnsurePoolRoot();
            if (poolRoot != null) slot.transform.SetParent(poolRoot, false);
            m_Free.Push(slot);
        }

        public void Assign(TMP_Text slot, object host)
        {
            if (!IsAlive(slot)) return;
            var parent = host as RectTransform;
            if (parent == null) return;
            slot.transform.SetParent(parent, false);
            slot.gameObject.SetActive(true);
        }

        public bool IsUsable(TMP_Text slot, object host)
        {
            var parent = host as RectTransform;
            if (parent == null) return false;
            if (!IsAlive(slot)) return false;
            return slot.transform.parent == parent && slot.gameObject.activeSelf;
        }

        /// <summary>
        /// 存在性合取判据（§5.3 的前半）：件还在、GameObject 还在、保存位仍然带 DontSave。
        /// 父节点与激活状态因 owner 而异，交给 <see cref="IsUsable"/>。
        /// </summary>
        private static bool IsAlive(TMP_Text slot)
        {
            if (slot == null) return false;
            if (slot.gameObject == null) return false;
            return (slot.hideFlags & HideFlags.DontSave) != 0;
        }

        private static void DestroySlot(TMP_Text slot)
        {
            if (slot == null) return;
            if (slot.gameObject == null) return;
            UnityEngine.Object.DestroyImmediate(slot.gameObject);
        }

        /// <summary>
        /// 拆池：把池根连同池内件一起硬销毁。<b>必须有这一步</b>——池根挂在 chart 自己下面而
        /// 工具清理只拆 <c>[XChartTools]</c>，不拆它就等于每回收重建一次漏一个池根在场景里。
        /// 已经发落出去的件不归这里：它们在各工具的宿主下，随工具根一起销毁。
        /// </summary>
        public void DestroyAll()
        {
            m_Free.Clear();
            if (m_PoolRoot == null) return;
            var rootObject = m_PoolRoot.gameObject;
            m_PoolRoot = null;
            if (rootObject != null) UnityEngine.Object.DestroyImmediate(rootObject);
        }

        private RectTransform EnsurePoolRoot()
        {
            if (m_PoolRoot != null) return m_PoolRoot;
            if (m_ChartRoot == null) return null;

            var go = new GameObject(PoolRootName, typeof(RectTransform));
            go.transform.SetParent(m_ChartRoot, false);
            go.hideFlags = XChartBase.CurrentFlags;
            m_PoolRoot = go.GetComponent<RectTransform>();
            return m_PoolRoot;
        }
    }
}
