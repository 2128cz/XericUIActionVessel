namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 单个成员的验证结果枚举
    /// </summary>
    public enum AvrgMemberCheckResult
    {
        /// <summary>未标记检查或不适用（跳过）</summary>
        Skipped = 0,

        /// <summary>检查通过，值有效</summary>
        Valid = 1,

        /// <summary>必填但为空</summary>
        Empty = 2,

        /// <summary>值在警告范围内</summary>
        Warning = 3,

        /// <summary>值在无效范围内</summary>
        Invalid = 4,
    }
}
