/// <summary>
/// 目录校验问题; 只描述事实, 由调用方决定如何呈现.
/// 归属: LayeredId (Runtime).
/// </summary>
public readonly struct LayeredIdIssue
{
    /// <summary>出问题的成员名; 空字符串表示问题属于整个目录.</summary>
    public string MemberName { get; }

    /// <summary>面向维护者的一句话说明.</summary>
    public string Message { get; }

    internal LayeredIdIssue(string memberName, string message)
    {
        MemberName = memberName;
        Message = message;
    }
}

