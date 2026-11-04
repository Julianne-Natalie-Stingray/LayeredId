/// <summary>
/// 目录成员名与路径的对应关系; 供 Inspector 展示与生成器使用.
/// 归属: LayeredId (Runtime).
/// </summary>
public readonly struct LayeredIdMember
{
    /// <summary>成员在代码中的名字; 重命名不影响路径.</summary>
    public string Name { get; }

    /// <summary>成员携带的逻辑路径; 空字符串表示未填写.</summary>
    public string Path { get; }

    internal LayeredIdMember(string name, string path)
    {
        Name = name;
        Path = path;
    }
}

