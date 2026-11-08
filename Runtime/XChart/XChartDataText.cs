using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表数据的文本序列化：<b>导出 / 导入 / 复制粘贴共用这一份格式定义</b>
    /// （规范 §3 的作用域纪律：不许导出写一套、粘贴再写一套）。
    ///
    /// <para><b>为什么是"行文本"而不是 JSON</b>：运行时程序集现在没有可依赖的 JSON 库，
    /// 手写一个 JSON 解析器等于自己造一个容易错的轮子；而行文本（制表符分隔）能直接丢进表格软件看、
    /// 在编辑器里逐行 diff、粘贴替换也不需要转义嵌套。字段名与顺序就是格式的一部分，见下。</para>
    ///
    /// <code>
    /// XCHART-DATA 2
    /// DIM  &lt;key&gt;  &lt;displayName&gt;  &lt;kind&gt;  &lt;format&gt;  &lt;ovMin:0/1&gt;  &lt;ovMax:0/1&gt;  &lt;manualMin&gt;  &lt;manualMax&gt;   ← 每个维度一行，顺序即维度下标
    /// REC  &lt;timestamp&gt;  &lt;v0&gt;  &lt;v1&gt; …          ← 每条记录一行；数值个数＝数值维个数
    /// </code>
    ///
    /// <para><b>为什么是 2</b>：第 1 版只带得出"两端一起覆盖"（两个数相等＝没覆盖），
    /// 而分侧覆盖（钉住最小、最大自由）是常态——两个标记位必须各自落进文件，否则导出再导回来
    /// 会把"只钉一端"读成"两端都钉"。旧文件（<see cref="LegacyHeaderLine"/>）仍按第 1 版口径读，
    /// 两种表头的字段布局不同，所以<b>版本必须来自表头本身</b>，不能靠数字猜。</para>
    ///
    /// <para><b>精度</b>：数值一律按往返精度（"R"）写、按不变文化读——<c>0.00</c> 那种写法会在
    /// 导出→导入之间静默丢精度，而这类丢精度在图上只表现为"点挪了一点点"，最难查。</para>
    ///
    /// <para><b>失败语义</b>：导入是<b>全有或全无</b>——任何一行不合法就整份拒绝并指出行号，
    /// 不半途替换掉一半数据（半份数据比没数据更坏）。</para>
    /// </summary>
    public static class XChartDataText
    {
        public const string HeaderLine = "XCHART-DATA 2";
        /// <summary>第 1 版表头：只带"两端一起"的覆盖（<c>&lt;min&gt; &lt;max&gt;</c> 直接跟在格式串后面），仍读得动</summary>
        public const string LegacyHeaderLine = "XCHART-DATA 1";
        public const string DimensionPrefix = "DIM";
        public const string RecordPrefix = "REC";

        private const char Tab = (char)9;
        private const char NewLine = (char)10;

        /// <summary>导出成文本（维度定义 + 全部记录）</summary>
        public static string Export(XChartDataModel model)
        {
            if (model == null) return HeaderLine + NewLine;

            var builder = new StringBuilder();
            builder.Append(HeaderLine).Append(NewLine);

            for (int i = 0; i < model.Dimensions.Count; i++)
            {
                var dimension = model.Dimensions[i];
                if (dimension == null) continue;
                var over = dimension.DomainOverride;
                builder.Append(DimensionPrefix).Append(Tab)
                    .Append(Escape(dimension.Key)).Append(Tab)
                    .Append(Escape(dimension.DisplayName)).Append(Tab)
                    .Append(dimension.Kind.ToString()).Append(Tab)
                    .Append(Escape(dimension.Format)).Append(Tab)
                    // 标记位与两端值<b>各写各的</b>：没钉住的那一端也要把数留着，
                    // 否则导回来就变成"从没填过"，下一次勾上时格子是空的
                    .Append(over.HasMin ? "1" : "0").Append(Tab)
                    .Append(over.HasMax ? "1" : "0").Append(Tab)
                    .Append(dimension.ManualRange.x.ToString("R", CultureInfo.InvariantCulture)).Append(Tab)
                    .Append(dimension.ManualRange.y.ToString("R", CultureInfo.InvariantCulture))
                    .Append(NewLine);
            }

            int numericCount = model.NumericDimensionCount;
            for (int r = 0; r < model.RecordCount; r++)
            {
                builder.Append(RecordPrefix).Append(Tab)
                    .Append(model.GetTimeAt(r).ToString("R", CultureInfo.InvariantCulture));
                for (int d = 0; d < numericCount; d++)
                {
                    builder.Append(Tab)
                        .Append(model.GetValueAt(r, d).ToString("R", CultureInfo.InvariantCulture));
                }

                var record = model.Records[r];
                for (int l = 0; l < record.Labels.Count; l++)
                    builder.Append(Tab).Append(Escape(record.Labels[l]));

                builder.Append(NewLine);
            }

            return builder.ToString();
        }

        /// <summary>把一行拆成字段（制表符分隔；不做引号语法——制表符与换行在写入时已被转义）</summary>
        public static string[] SplitFields(string line)
        {
            if (string.IsNullOrEmpty(line)) return new string[0];
            var fields = new List<string>();
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != Tab) continue;
                fields.Add(Unescape(line.Substring(start, i - start)));
                start = i + 1;
            }
            fields.Add(Unescape(line.Substring(start)));
            return fields.ToArray();
        }

        /// <summary>
        /// 导入：<paramref name="replace"/> 为真＝整表替换，为假＝追加。
        /// <b>先解析成临时结果、全部合法才落到目标上</b>——失败时不留下半份数据。
        /// </summary>
        public static bool TryImport(string text, XChartDataModel target, bool replace, out string error)
        {
            error = null;
            if (target == null) { error = "没有目标数据模型"; return false; }
            if (text == null) { error = "文本为空"; return false; }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var dimensions = new List<XChartDimensionDefinition>();
            var timestamps = new List<double>();
            var recordValues = new List<float[]>();
            var recordLabels = new List<string[]>();
            bool sawHeader = false;
            bool legacyLayout = false;   // 字段布局由表头定，不由"像不像数字"定

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                if (!sawHeader)
                {
                    if (line == HeaderLine) legacyLayout = false;
                    else if (line == LegacyHeaderLine) legacyLayout = true;
                    else
                    {
                        error = "第 " + (i + 1) + " 行不是表头（应以 " + HeaderLine + " 或 " + LegacyHeaderLine + " 开头）";
                        return false;
                    }
                    sawHeader = true;
                    continue;
                }

                string[] fields = SplitFields(line);
                if (fields.Length == 0) continue;

                if (fields[0] == DimensionPrefix)
                {
                    if (fields.Length < 4)
                    {
                        error = "第 " + (i + 1) + " 行的维度定义少了字段（需要 key / 显示名 / 种类）";
                        return false;
                    }

                    XChartDimensionKind kind;
                    if (!TryParseKind(fields[3], out kind))
                    {
                        error = "第 " + (i + 1) + " 行的维度种类无法识别：" + fields[3];
                        return false;
                    }

                    var dimension = new XChartDimensionDefinition(fields[1], fields[2], kind);
                    if (fields.Length >= 5 && !string.IsNullOrEmpty(fields[4])) dimension.Format = fields[4];

                    // 覆盖段的长度由版本定：v1 是 `<min> <max>`（两端一起），v2 是 `<ovMin> <ovMax> <min> <max>`。
                    // 只给"少一截"的行报错，不静默丢掉半个覆盖——字段数对不上就是这份文本被改坏了。
                    int full = legacyLayout ? 7 : 9;
                    if (fields.Length != 4 && fields.Length != 5 && fields.Length != full)
                    {
                        error = "第 " + (i + 1) + " 行的维度定义字段数不对（要 4、5 或 " + full + " 个，现在 "
                            + fields.Length + " 个）";
                        return false;
                    }

                    if (fields.Length == full)
                    {
                        double manualMin, manualMax;
                        if (legacyLayout)
                        {
                            // 第 1 版没有分侧标记：两个数相等＝没覆盖（那正是当时的"一起覆盖"口径）
                            if (!TryParseFinite(fields[5], out manualMin) || !TryParseFinite(fields[6], out manualMax))
                            {
                                error = "第 " + (i + 1) + " 行的覆盖范围不是有限数字（" + fields[5] + " / " + fields[6] + "）";
                                return false;
                            }
                            dimension.SetRangeOverride(manualMin, manualMax);
                        }
                        else
                        {
                            bool pinMin, pinMax;
                            if (!TryParseFlag(fields[5], out pinMin) || !TryParseFlag(fields[6], out pinMax))
                            {
                                error = "第 " + (i + 1) + " 行的覆盖标记只能是 0 或 1（" + fields[5] + " / " + fields[6] + "）";
                                return false;
                            }
                            if (!TryParseFinite(fields[7], out manualMin) || !TryParseFinite(fields[8], out manualMax))
                            {
                                error = "第 " + (i + 1) + " 行的覆盖端点不是有限数字（" + fields[7] + " / " + fields[8] + "）";
                                return false;
                            }
                            dimension.SetDomainOverride(pinMin, manualMin, pinMax, manualMax);
                        }
                    }

                    dimensions.Add(dimension);
                    continue;
                }

                if (fields[0] == RecordPrefix)
                {
                    if (dimensions.Count == 0)
                    {
                        error = "第 " + (i + 1) + " 行是记录，但前面没有任何 DIM 行（先声明维度）";
                        return false;
                    }

                    // 少字段的行要在门口拒：直接读 fields[1] 会抛出去，而"导入失败"的语义是
                    // **指出行号并整份拒绝**，不是把异常丢给调用方（半份数据比没数据更坏）
                    if (fields.Length < 2)
                    {
                        error = "第 " + (i + 1) + " 行的记录少了时间戳字段";
                        return false;
                    }

                    double timestamp;
                    if (!TryParseDouble(fields[1], out timestamp))
                    {
                        error = "第 " + (i + 1) + " 行的时间戳不是数字";
                        return false;
                    }

                    int numericCount = 0;
                    for (int d = 0; d < dimensions.Count; d++)
                        if (dimensions[d].Kind == XChartDimensionKind.Number) numericCount++;

                    // 标签段的长度由**标签列个数**定（同一处规则：XChartColumnMath.LabelCount）。
                    // 多出来的字符串没有列叫得出它的名字：它落进记录后窗口显示得出来、元素却读不到，
                    // 所以这里当场拒收并点名行号，而不是"先收下再说"。
                    int labelCount = XChartColumnMath.LabelCount(dimensions);
                    int labelFields = fields.Length - 2 - numericCount;

                    if (fields.Length - 2 < numericCount)
                    {
                        error = "第 " + (i + 1) + " 行的数值个数（" + (fields.Length - 2) + "）少于数值维个数（" + numericCount + "）";
                        return false;
                    }
                    if (labelFields != labelCount)
                    {
                        error = "第 " + (i + 1) + " 行的标签个数（" + labelFields + "）与标签列个数（" + labelCount + "）对不上";
                        return false;
                    }

                    var values = new float[numericCount];
                    for (int d = 0; d < numericCount; d++)
                    {
                        double value;
                        if (!TryParseDouble(fields[2 + d], out value))
                        {
                            error = "第 " + (i + 1) + " 行第 " + (d + 1) + " 个数值不是数字：" + fields[2 + d];
                            return false;
                        }
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            error = "第 " + (i + 1) + " 行第 " + (d + 1) + " 个数值是 NaN/Infinity，记录表不收这种值";
                            return false;
                        }
                        values[d] = (float)value;
                    }

                    var labels = new string[labelCount];
                    for (int l = 0; l < labelCount; l++) labels[l] = fields[2 + numericCount + l];

                    timestamps.Add(timestamp);
                    recordValues.Add(values);
                    recordLabels.Add(labels);
                    continue;
                }

                error = "第 " + (i + 1) + " 行无法识别（应以 " + DimensionPrefix + " 或 " + RecordPrefix + " 开头）";
                return false;
            }

            if (!sawHeader) { error = "文本只有空白（缺少表头）"; return false; }
            if (dimensions.Count == 0) { error = "没有任何 DIM 行"; return false; }

            // 到这里才动目标：先定义维度（会重排既有记录的数值个数），再灌记录
            target.SetDimensions(dimensions);
            if (replace) target.Clear();

            for (int i = 0; i < timestamps.Count; i++)
            {
                if (!target.TryAddRecord(timestamps[i], recordValues[i], recordLabels[i]))
                {
                    error = "第 " + (i + 1) + " 条记录没能写进模型（时间必须非递减、数值个数要对得上）";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseKind(string text, out XChartDimensionKind kind)
        {
            kind = XChartDimensionKind.Number;
            if (string.IsNullOrEmpty(text)) return false;
            switch (text)
            {
                case "Number": kind = XChartDimensionKind.Number; return true;
                case "Time": kind = XChartDimensionKind.Time; return true;
                case "Label": kind = XChartDimensionKind.Label; return true;
                default:
                    int raw;
                    if (int.TryParse(text, out raw) && raw >= 0 && raw <= 2)
                    {
                        kind = (XChartDimensionKind)raw;
                        return true;
                    }
                    return false;
            }
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 覆盖端点：必须是<b>有限数</b>。文本里写得出 <c>NaN</c> / <c>Infinity</c>，而这两个数一旦当成
        /// 范围端点，整条轴会安静地什么都不画——那是"导入成功但图空了"最难查的一条路，所以在门口就拒。
        /// </summary>
        private static bool TryParseFinite(string text, out double value)
        {
            if (!TryParseDouble(text, out value)) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) { value = 0.0; return false; }
            return true;
        }

        /// <summary>分侧覆盖的标记位：只认 <c>0</c> / <c>1</c>（别的写法一律算这份文本被改坏了）</summary>
        private static bool TryParseFlag(string text, out bool value)
        {
            value = false;
            if (text == "0") return true;
            if (text == "1") { value = true; return true; }
            return false;
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace(Tab, ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Unescape(string text)
        {
            return text ?? string.Empty;
        }
    }
}