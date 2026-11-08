using System;
using TMPro;
using UnityEngine;
using XericUI.Core;
using XericUI.XChart.Space;

namespace XericUI.XChart.Text
{
    /// <summary>
    /// 数据标签元素：一条记录一个标签，标签读的是那一行在<b>某一标签列</b>上的字符串。
    ///
    /// <para><b>1:1 是结构保证而不是约定</b>：记录身上的标签数组长度＝标签列个数，
    /// 所以这一列读得到的就是那一行的那一格；比列更多的字符串没有列叫得出它的名字，
    /// 它在窗口里显示得出来、元素读不到（口径只有一份：<see cref="XChartColumnMath"/>）。
    /// 一条记录配几个数值维＋一个字符串维，画面上就是"点＋它自己的名字"。</para>
    ///
    /// <para><b>位置从哪儿来</b>：和散点/圆柱/矢量场<b>同一个</b>空间与同一份折法
    /// （<see cref="XChartCubeFoldMath"/>），显示范围只定标尺；超范围怎么办由这一档自己决定（§32）。
    /// <b>显示从哪儿来</b>：文本槽位池（运行时里唯一造 TMP 标签的地方），一件对应一条记录。</para>
    ///
    /// <para><b>锚点和对齐出自同一张表</b>：贴在左下角却把字居中，字就会横跨在那一点两侧。
    /// 锚点决定矩形从哪一侧从点起排，对齐由同一个锚点算出来（<see cref="XChartLabelAnchorMath"/>）。</para>
    ///
    /// 子节点结构：
    ///   [Labels]
    ///     └─ GeneratedTexts（文本槽位的宿主）
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Text/Data Label Tool")]
    public class ChartToolLabels : XChartToolBase
    {
        // ── 数据装配 ──────────────────────────────────────────────

        private int m_LabelSlot;
        /// <summary>读第几个<b>标签列</b>（不是表里的行号；换算只有 <see cref="XChartColumnMath.LabelSlotOf"/> 那一处）</summary>
        public int LabelSlot { get => m_LabelSlot; set { m_LabelSlot = value; MarkDirty(); } }

        private int m_DimensionX;
        /// <summary>标签挂在哪条记录的哪一个位置：三个数值维下标（显示范围归 chart 的数据维度行）</summary>
        public int DimensionX { get => m_DimensionX; set { m_DimensionX = value; MarkDirty(); } }
        private int m_DimensionY = 1;
        public int DimensionY { get => m_DimensionY; set { m_DimensionY = value; MarkDirty(); } }
        private int m_DimensionZ = 2;
        public int DimensionZ { get => m_DimensionZ; set { m_DimensionZ = value; MarkDirty(); } }

        private XChartToolTransform m_Transform = XChartToolTransform.Identity;
        public XChartToolTransform Transform { get => m_Transform; set { m_Transform = value; MarkDirty(); } }

        private XChartValueOutOfRange m_OutOfRange = XChartValueOutOfRange.RenderAsIs;
        /// <summary>标签的位置受显示范围之外的影响时怎么办（§32：范围只定标尺，画不画由数据图元自己决定）</summary>
        public XChartValueOutOfRange OutOfRange { get => m_OutOfRange; set { m_OutOfRange = value; MarkDirty(); } }

        // ── 外观 ──────────────────────────────────────────────────

        private XChartLabelAnchor m_Anchor = XChartLabelAnchor.UpperCenter;
        /// <summary>标签矩形相对它那一点的位置（9 档）。<b>文字对齐由同一档算出来</b>，不另给一个字段</summary>
        public XChartLabelAnchor Anchor { get => m_Anchor; set { m_Anchor = value; MarkDirty(); } }

        private Vector2 m_Offset = new Vector2(0f, 6f);
        /// <summary>从那个点再挪多少像素（非有限数按零算：NaN 进 anchoredPosition 会把这一件字整个挪出画面而不报错）</summary>
        public Vector2 Offset { get => m_Offset; set { m_Offset = value; MarkDirty(); } }

        private int m_FontSize = 14;
        public int FontSize { get => m_FontSize; set { m_FontSize = Mathf.Max(1, value); MarkDirty(); } }

        private Color m_LabelColor = Color.white;
        public Color LabelColor { get => m_LabelColor; set { m_LabelColor = value; MarkDirty(); } }

        private float m_OutlineWidth;
        public float OutlineWidth { get => m_OutlineWidth; set { m_OutlineWidth = Mathf.Max(0f, value); MarkDirty(); } }

        private Vector2 m_RectSize = new Vector2(160f, 30f);
        public Vector2 RectSize { get => m_RectSize; set { m_RectSize = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y)); MarkDirty(); } }

        private bool m_Wrap;
        public bool Wrap { get => m_Wrap; set { m_Wrap = value; MarkDirty(); } }

        // ── 内部 ──────────────────────────────────────────────────

        private bool m_GeometryDirty = true;
        private long m_LastDataVersion = -1;
        private int m_LastSpaceVersion = -1;
        private Matrix4x4 m_Composed = Matrix4x4.identity;

        private int m_Records;
        private int m_Written;
        private int m_Cleared;
        private int m_Empty;
        private int m_Hidden;
        private int m_Skipped;

        public override void MarkDirty() => m_GeometryDirty = true;

        public override void OnRender()
        {
            base.OnRender();
            var space = Context?.Space;
            if (space == null) return;

            var data = Data;
            bool spaceChanged = space.ProjectionVersion != m_LastSpaceVersion;
            bool dataChanged = data != null && data.Version != m_LastDataVersion;
            if (!m_GeometryDirty && !spaceChanged && !dataChanged) return;

            if (spaceChanged || dataChanged) m_GeometryDirty = true;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = data != null ? data.Version : -1;
            m_Composed = space.Compose(Transform);

            if (data == null || !data.HasData)
            {
                m_Records = 0;
                RequestTextSlots(0);
                ClearTexts();
                m_GeometryDirty = false;
                return;
            }

            int records = data.RecordCount;
            m_Records = records;
            RequestTextSlots(records);

            double minX, maxX, minY, maxY, minZ, maxZ;
            ResolveRange(DimensionX, out minX, out maxX);
            ResolveRange(DimensionY, out minY, out maxY);
            ResolveRange(DimensionZ, out minZ, out maxZ);

            var texts = Texts;
            m_Written = 0;
            m_Cleared = 0;
            m_Empty = 0;
            m_Hidden = 0;
            m_Skipped = 0;

            for (int i = 0; i < records; i++)
            {
                bool ready = i < texts.Count && texts[i] != null;

                string text;
                if (!data.TryGetLabelAt(i, LabelSlot, out text))
                {
                    // 这一列没有这一格：数据不可用（不是"标签是空的"）
                    m_Skipped++;
                    if (ready) BlankSlot(texts[i]);
                    continue;
                }

                if (string.IsNullOrEmpty(text))
                {
                    // 那一格是空的（被清空过）：这一件不画字，照实擦掉上一轮的字
                    m_Empty++;
                    if (ready) BlankSlot(texts[i]);
                    continue;
                }

                double x = data.GetValueAt(i, DimensionX);
                double y = data.GetValueAt(i, DimensionY);
                double z = data.GetValueAt(i, DimensionZ);

                // 三个 out 先给初值：`&&` 短路时后面那两次调用根本没跑，编译器认不出"折得成就说明三个都赋过"
                // （散点那一处是 `if (!a || !b || !c) continue;` 的写法，所以它没这个问题）
                float fx = 0f, fy = 0f, fz = 0f;
                bool foldable = XChartCubeFoldMath.TryRatio01(x, minX, maxX, OutOfRange, out fx)
                    && XChartCubeFoldMath.TryRatio01(y, minY, maxY, OutOfRange, out fy)
                    && XChartCubeFoldMath.TryRatio01(z, minZ, maxZ, OutOfRange, out fz);

                if (!foldable)
                {
                    // 三个数都可用却被拒 ⇒ 是这一档主动不画；否则是坏数据（本来就不该画，另算）
                    if (!IsBad(x) && !IsBad(y) && !IsBad(z)) m_Hidden++;
                    else m_Skipped++;
                    if (ready) BlankSlot(texts[i]);
                    continue;
                }

                if (!ready) continue;   // 件还没到位：欠着件不算"这一行没标签"（pending= 那一行说的是它）

                Vector2 local = space.Project(ChartToolSpace.NormalizedToCube(new Vector3(fx, fy, fz)), m_Composed)
                    + XChartLabelAnchorMath.SafeOffset(Offset);

                WriteLabel(texts[i], local, text);
                m_Written++;
            }

            // 槽位数比记录数多（上一轮表更长）时多出来的那些必须擦干净，否则上一轮的标签留在原地
            for (int i = records; i < texts.Count; i++)
                if (texts[i] != null) BlankSlot(texts[i]);

            m_GeometryDirty = texts.Count < records;
        }

        private void BlankSlot(TMP_Text slot)
        {
            slot.text = string.Empty;
            m_Cleared++;
        }

        private void WriteLabel(TMP_Text label, Vector2 local, string text)
        {
            Vector2 anchor = XChartLabelAnchorMath.AnchorOf(Anchor);

            label.text = text;
            label.fontSize = FontSize;
            label.color = LabelColor;
            label.enableWordWrapping = Wrap;
            label.outlineWidth = OutlineWidth;
            label.alignment = XChartLabelAnchorMath.AlignmentFor(Anchor);

            var rect = label.rectTransform;
            rect.sizeDelta = RectSize;
            // 锚点写在 pivot 上：这样"那一格从点起排"和"点正好落在矩形的这一角"是同一件事。
            // anchorMin/Max 留在池的默认（中心），否则 anchoredPosition 的参照点会跟着换，
            // 同一个读数在两种锚法下答得不同。
            rect.pivot = anchor;
            rect.anchoredPosition = local;
        }

        private void ClearTexts()
        {
            var texts = Texts;
            m_Cleared = 0;
            for (int i = 0; i < texts.Count; i++)
                if (texts[i] != null) BlankSlot(texts[i]);
        }

        /// <summary>显示范围只问上下文要（覆盖优先、否则该维聚合）：元素自己存一份范围就是"标签和轴对不上"的来路</summary>
        private void ResolveRange(int dimension, out double min, out double max)
        {
            var context = Context;
            if (context != null && context.TryResolveDomain(dimension, out min, out max)) return;
            min = 0.0;
            max = 1.0;
        }

        private static bool IsBad(double value)
            => double.IsNaN(value) || double.IsInfinity(value);

        protected override void OnDispose()
        {
            m_Records = 0;
            m_Written = 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 报表里这一行只印"这一帧到底有没有写、没写的话卡在哪"：<b>数据侧</b>（skipped/hidden/empty）
        /// 与<b>槽位侧</b>（want/have/pending）必须分开，否则"这一行没标签"和"件还没发落"在画面上同形。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" labelSlot=").Append(LabelSlot)
              .Append(" dim=").Append(DimensionX).Append('/').Append(DimensionY).Append('/').Append(DimensionZ)
              .Append(" records=").Append(m_Records)
              .Append(" want=").Append(RequestedTextCount)
              .Append(" have=").Append(Texts.Count)
              .Append(" pending=").Append(TextSlotsPending)
              .Append(" written=").Append(m_Written)
              .Append(" cleared=").Append(m_Cleared)
              .Append(" empty=").Append(m_Empty)
              .Append(" hidden=").Append(m_Hidden)
              .Append(" skipped=").Append(m_Skipped)
              .Append(" out=").Append(OutOfRange.ToString().ToLower())
              .Append(" anchor=").Append(Anchor.ToString().ToLower())
              .Append(" wrap=").Append(Wrap);
        }
#endif
    }
}
