/// <summary>
/// 分层标识的值契约: 只暴露路径与有效性, 供工具链与泛型约束使用.
/// 归属: LayeredId (Runtime).
/// 不负责: 相等性实现; 由各域 ID 类型自行提供.
/// </summary>
public interface ILayeredId
{
    /// <summary>逻辑路径, 以 '/' 分层; 空字符串表示未设置.</summary>
    string Path { get; }

    /// <summary>路径非空白即为有效; 不校验是否存在于目录.</summary>
    bool IsValid { get; }
}

