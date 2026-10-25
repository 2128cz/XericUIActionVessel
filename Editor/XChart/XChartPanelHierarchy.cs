using System;
using System.Collections.Generic;

namespace XericUI.XChart.Editor
{
    /// <summary>
    /// 折叠层级的判定：<b>纯数据，不碰 UIElements</b>。
    ///
    /// <para>为什么单独一支：面板要"互斥标记只打在最深那一层"，而"谁是谁的父级"是装配完才定的
    /// （<c>GroupFor</c> 的同名合并、跨父级不合并都会改挂接位置）。这段规则要是散在控件代码里，
    /// 就只能靠眼睛验收；抽出来之后它能离线跑（引擎的 mono 造不出真 <c>VisualElement</c>——
    /// 它的静态构造要撞 <c>UIElementsRuntimeUtilityNative</c>，所以这里对树一无所知，
    /// 只接两条注入的访问器：<c>parentOf</c> 往上一格、<c>keyOf</c> 读一个格子带的 key）。</para>
    ///
    /// <para><b>离线能看见的与看不见的</b>：能看见父级识别、深度、叶子判定这三步算术；
    /// 看不见真控件的 <c>parent</c>/<c>userData</c> 到底填没填——那一层由面板自己在报表里印
    /// <c>groups/deepest/depth</c> 三个读数作证（数错了会在 <c>HIERARCHY-NO-PARENT</c> 这类
    /// 判决行里点名，不是"没打印所以没事"）。</para>
    /// </summary>
    public static class XChartPanelHierarchy
    {
        /// <summary>一块折叠板。Box 是它的盒子根（key 钉在盒子根上，不是钉在标题上）。</summary>
        public sealed class Node<T> where T : class
        {
            public string Key;
            public T Box;

            public string ParentKey;
            public int Depth;
            public bool IsLeaf;
        }

        /// <summary>
        /// 就地算出每个节点的 <see cref="Node{T}.ParentKey"/> / <see cref="Node{T}.Depth"/> /
        /// <see cref="Node{T}.IsLeaf"/>。
        ///
        /// <para><b>父级 = 沿 parent 往上走，第一个 key 在本轮清单里的格子</b>。中途那些没有 key 的
        /// 容器（Body、装字段的临时壳）直接跳过——所以深度按"折叠块"数，不按控件层数。</para>
        ///
        /// <para><b>最深的一层 = 不是任何块的父级的那些块</b>（叶子）。注意这不是"全局最大深度"：
        /// 一条分支只有一层（基础 → 调试），另一条有两条（基础 → 数据 → 维度 0），两边的叶子都算
        /// "它那条分支里最深的一层"，都要打标记；按全局最大深度挑就会把"调试"漏成自由块。</para>
        /// </summary>
        public static void Compute<T>(IList<Node<T>> nodes, Func<T, T> parentOf, Func<T, string> keyOf)
            where T : class
        {
            var byKey = new Dictionary<string, Node<T>>();
            foreach (var node in nodes) byKey[node.Key] = node;

            var parents = new HashSet<string>();
            foreach (var node in nodes)
            {
                node.ParentKey = NearestAncestorKey(node.Box, parentOf, keyOf, byKey);
                if (node.ParentKey != null) parents.Add(node.ParentKey);
            }

            foreach (var node in nodes)
            {
                node.Depth = DepthOf(node, byKey);
                node.IsLeaf = !parents.Contains(node.Key);
            }
        }

        private static string NearestAncestorKey<T>(T box, Func<T, T> parentOf,
            Func<T, string> keyOf, Dictionary<string, Node<T>> byKey) where T : class
        {
            for (T node = box == null ? null : parentOf(box); node != null; node = parentOf(node))
            {
                string key = keyOf(node);
                if (key != null && byKey.ContainsKey(key)) return key;
            }
            return null;
        }

        private static int DepthOf<T>(Node<T> node, Dictionary<string, Node<T>> byKey) where T : class
        {
            int depth = 0;
            string key = node.ParentKey;
            Node<T> parent;
            // 父级先全部算完才走到这里，所以沿 ParentKey 往上爬是有限步（挂接是棵树，不会绕圈）
            while (key != null && byKey.TryGetValue(key, out parent))
            {
                depth++;
                key = parent.ParentKey;
            }
            return depth;
        }
    }
}
