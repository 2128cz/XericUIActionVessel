namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 值校验状态枚举
    /// Normal = 正常（通过校验），Warning = 警告（值在警告范围但放行），Invalid = 无效（值被驳回不写入）
    /// </summary>
    public enum ValidationState
    {
        Normal = 0,
        Warning = 1,
        Invalid = 2
    }
}
