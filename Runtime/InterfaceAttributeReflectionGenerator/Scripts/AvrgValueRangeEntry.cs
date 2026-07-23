using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 值范围规则条目
    /// 定义某个类型（由 CheckKey 标识）的空/无效/警告/合理值范围
    /// </summary>
    [Serializable]
    public class AvrgValueRangeEntry
    {
        /// <summary>
        /// 类型标识符（默认使用 Type.FullName，如 "System.Single", "System.Int32"）
        /// 也支持自定义字符串以匹配特定语义
        /// </summary>
        public string key = "";

        // ===== 空值范围 =====

        /// <summary>
        /// 是否跳过空值检查（如 bool 类型无法判断空）
        /// </summary>
        public bool skipEmptyCheck = false;

        /// <summary>
        /// 视为空的最小值（>= 此值视为空）
        /// </summary>
        public float emptyRangeMin = float.MinValue;

        /// <summary>
        /// 视为空的最大值（<= 此值视为空）
        /// </summary>
        public float emptyRangeMax = float.MaxValue;

        // ===== 合理值范围（最高优先级） =====

        /// <summary>
        /// 合理值最小值
        /// </summary>
        public float reasonableRangeMin = float.MinValue;

        /// <summary>
        /// 合理值最大值
        /// </summary>
        public float reasonableRangeMax = float.MaxValue;

        // ===== 无效值范围 =====

        /// <summary>
        /// 无效值最小值
        /// </summary>
        public float invalidRangeMin = float.MinValue;

        /// <summary>
        /// 无效值最大值
        /// </summary>
        public float invalidRangeMax = float.MaxValue;

        // ===== 警告值范围 =====

        /// <summary>
        /// 警告值最小值
        /// </summary>
        public float warningRangeMin = float.MinValue;

        /// <summary>
        /// 警告值最大值
        /// </summary>
        public float warningRangeMax = float.MaxValue;

        // ===== 字符串相关 =====

        /// <summary>
        /// 警告关键词列表（仅字符串类型使用）
        /// </summary>
        public List<string> warningKeywords = new List<string>();

        // ===== 便捷判断方法 =====

        /// <summary>
        /// 将值转为 float 进行比较（bool→0/1, 数值→直接转, 字符串→长度）
        /// </summary>
        public static float ToComparableFloat(object value)
        {
            if (value == null) return float.NaN;

            switch (value)
            {
                case float f: return f;
                case double d: return (float)d;
                case int i: return i;
                case long l: return l;
                case short s: return s;
                case byte b: return b;
                case uint ui: return ui;
                case bool bVal: return bVal ? 1f : 0f;
                case string str: return str.Length;
                default: return float.NaN;
            }
        }

        /// <summary>
        /// 判断值是否为空
        /// </summary>
        public bool IsEmpty(object value)
        {
            if (skipEmptyCheck) return false;

            // 引用类型 null
            if (value == null) return true;

            // 字符串 null/空
            if (value is string str) return string.IsNullOrEmpty(str);

            // 数值范围判断
            float f = ToComparableFloat(value);
            if (float.IsNaN(f)) return false; // 无法判断的类型跳过

            return f >= emptyRangeMin && f <= emptyRangeMax;
        }

        /// <summary>
        /// 判断值是否在合理范围内
        /// </summary>
        public bool IsReasonable(object value)
        {
            float f = ToComparableFloat(value);
            if (float.IsNaN(f)) return false;
            return f >= reasonableRangeMin && f <= reasonableRangeMax;
        }

        /// <summary>
        /// 判断值是否在无效范围内
        /// </summary>
        public bool IsInvalid(object value)
        {
            float f = ToComparableFloat(value);
            if (float.IsNaN(f)) return false;
            return f >= invalidRangeMin && f <= invalidRangeMax;
        }

        /// <summary>
        /// 判断值是否在警告范围内
        /// </summary>
        public bool IsWarning(object value)
        {
            // 先检查关键词（字符串）
            if (value is string str && warningKeywords.Count > 0)
            {
                foreach (var kw in warningKeywords)
                {
                    if (str.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // 数值范围
            float f = ToComparableFloat(value);
            if (float.IsNaN(f)) return false;
            return f >= warningRangeMin && f <= warningRangeMax;
        }
    }
}
