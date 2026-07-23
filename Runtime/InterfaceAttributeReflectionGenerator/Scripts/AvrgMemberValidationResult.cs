using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 单个成员的详细验证结果
    /// </summary>
    public struct AvrgMemberValidationResult
    {
        /// <summary>验证的元数据</summary>
        public AvrgMemberMetaData MetaData { get; set; }

        /// <summary>验证结果</summary>
        public AvrgMemberCheckResult Result { get; set; }

        /// <summary>当前值（字符串形式，便于显示）</summary>
        public string CurrentValueString { get; set; }

        /// <summary>验证消息（说明原因）</summary>
        public string Message { get; set; }

        /// <summary>是否通过验证</summary>
        public bool IsPassed => Result == AvrgMemberCheckResult.Valid
                                || Result == AvrgMemberCheckResult.Skipped
                                || Result == AvrgMemberCheckResult.Warning; // Warning 视为通过但需注意
    }

    /// <summary>
    /// 完整验证结果集合
    /// </summary>
    public class AvrgValidationResultSet
    {
        /// <summary>所有成员的验证结果</summary>
        public List<AvrgMemberValidationResult> Results { get; set; } = new List<AvrgMemberValidationResult>();

        /// <summary>未验证通过的条目数（Empty + Invalid）</summary>
        public int FailedCount
        {
            get
            {
                int count = 0;
                foreach (var r in Results)
                {
                    if (r.Result == AvrgMemberCheckResult.Empty || r.Result == AvrgMemberCheckResult.Invalid)
                        count++;
                }
                return count;
            }
        }

        /// <summary>警告条目数</summary>
        public int WarningCount
        {
            get
            {
                int count = 0;
                foreach (var r in Results)
                {
                    if (r.Result == AvrgMemberCheckResult.Warning)
                        count++;
                }
                return count;
            }
        }

        /// <summary>是否全部通过（无失败项）</summary>
        public bool IsAllPassed => FailedCount == 0;
    }
}
