using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 分层标识目录基类: 反射自身声明的 const string 成员, 生成路径清单与校验问题.
/// 归属: LayeredId (Runtime).
/// 不负责: 路径取值与分层语义; 由各域目录的 const 成员决定.
/// </summary>
public abstract class LayeredIdCatalog
{
    private const BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<Type, CatalogData> Cache = new();

    /// <summary>去重并按 Ordinal 排序后的全部路径; 按类型缓存, 首次访问触发反射.</summary>
    public IReadOnlyList<string> All => AllOf(GetType());

    /// <summary>区分大小写的全等比较, 不做分隔符归一化.</summary>
    public bool Contains(string path) => Contains(GetType(), path);

    /// <summary>返回类型对应的路径清单; 不要求类型带 LayeredIdCatalogAttribute.</summary>
    public static IReadOnlyList<string> AllOf(Type catalogType) => DataOf(catalogType).Paths;

    /// <summary>路径为空或不在目录内时返回 false.</summary>
    public static bool Contains(Type catalogType, string path)
        => !string.IsNullOrWhiteSpace(path) && DataOf(catalogType).PathSet.Contains(path);

    /// <summary>按声明顺序返回成员名与路径的对应关系.</summary>
    public static IReadOnlyList<LayeredIdMember> MembersOf(Type catalogType)
        => DataOf(catalogType).Members;

    /// <summary>返回空路径、重复路径与空目录等问题; 无问题时返回空列表.</summary>
    public static IReadOnlyList<LayeredIdIssue> Validate(Type catalogType)
        => DataOf(catalogType).Issues;

    private static CatalogData DataOf(Type catalogType)
    {
        if (catalogType == null)
            throw new ArgumentNullException(nameof(catalogType));

        if (!typeof(LayeredIdCatalog).IsAssignableFrom(catalogType))
        {
            throw new ArgumentException(
                $"{catalogType.Name} 未继承 {nameof(LayeredIdCatalog)}. ", nameof(catalogType));
        }

        if (Cache.TryGetValue(catalogType, out CatalogData cached))
            return cached;

        CatalogData built = Build(catalogType);
        Cache.Add(catalogType, built);
        return built;
    }

    private static CatalogData Build(Type catalogType)
    {
        var members = new List<LayeredIdMember>();
        var issues = new List<LayeredIdIssue>();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (FieldInfo field in catalogType.GetFields(MemberFlags))
        {
            // 只认编译期常量: static readonly 会触发静态构造, 且不保证初始化顺序.
            if (field.FieldType != typeof(string) || !field.IsLiteral)
                continue;

            string path = field.GetRawConstantValue() as string ?? string.Empty;
            members.Add(new LayeredIdMember(field.Name, path));

            if (string.IsNullOrWhiteSpace(path))
            {
                issues.Add(new LayeredIdIssue(field.Name, "路径为空. "));
                continue;
            }

            if (owners.TryGetValue(path, out string owner))
            {
                issues.Add(new LayeredIdIssue(field.Name, $"路径 {path} 与 {owner} 重复. "));
                continue;
            }

            owners.Add(path, field.Name);
        }

        if (members.Count == 0)
            issues.Add(new LayeredIdIssue(string.Empty, "目录内没有任何 public const string 成员. "));

        var paths = new string[owners.Count];
        owners.Keys.CopyTo(paths, 0);
        Array.Sort(paths, StringComparer.Ordinal);

        return new CatalogData
        {
            Paths = Array.AsReadOnly(paths),
            PathSet = new HashSet<string>(paths, StringComparer.Ordinal),
            Members = members.AsReadOnly(),
            Issues = issues.AsReadOnly(),
        };
    }

    private sealed class CatalogData
    {
        public IReadOnlyList<string> Paths;
        public HashSet<string> PathSet;
        public IReadOnlyList<LayeredIdMember> Members;
        public IReadOnlyList<LayeredIdIssue> Issues;
    }
}

