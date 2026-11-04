# Layered Identifier

分层标识的通用底座：ID 值类型 + 目录类 + Inspector 抽屉 + 校验窗口。

当前以内嵌包形式位于 `Packages/com.grignardreagent.layeredid`，版本 `0.1.0`。程序集名是 `GrignardReagent.LayeredId` / `GrignardReagent.LayeredId.Editor`，将来发布到远端时引用方无需改动。

## 结构

```text
Runtime/   ILayeredId、LayeredIdCatalog、[LayeredIdCatalog]、LayeredIdMember、LayeredIdIssue
Editor/    LayeredIdDrawer、LayeredIdCatalogWindow
Tests/     EditMode：排序、Ordinal、空路径、重复、空目录
```

运行时程序集 **不引用 UnityEngine**，且 `autoReferenced = true`：项目内程序集可直接使用，无需显式引用。`Editor` 程序集同样 `autoReferenced = true`。

## 一个域需要三个文件

```csharp
// QuestId.cs
[Serializable]
public struct QuestId : ILayeredId
{
    [SerializeField] private string path;   // 字段名必须是 path

    public string Path => path ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(path);

    public static QuestId None => default;
    public static QuestId From(string path) => new() { path = path ?? string.Empty };

    public bool Equals(QuestId other) => string.Equals(Path, other.Path, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is QuestId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Path);
    public override string ToString() => IsValid ? Path : "None";
    public static bool operator ==(QuestId left, QuestId right) => left.Equals(right);
    public static bool operator !=(QuestId left, QuestId right) => !left.Equals(right);
}
```

```csharp
// QuestCatalog.cs
[LayeredIdCatalog]
public sealed class QuestCatalog : LayeredIdCatalog
{
    public const string MainIntro = "Quest/Main/Intro";
    public const string MainEnd   = "Quest/Main/End";
}
```

```csharp
// Editor/QuestIdDrawer.cs
[CustomPropertyDrawer(typeof(QuestId))]
internal sealed class QuestIdDrawer : LayeredIdDrawer
{
    protected override Type CatalogType => typeof(QuestCatalog);
}
```

asmdef：运行时已 `autoReferenced`，ID 所在程序集无需显式引用；抽屉所在编辑器程序集需要引用 `GrignardReagent.LayeredId.Editor`（模板的 `Basic2DPlatformer.Editor` 已引用）。

## 从新增到使用

1. 按上面的模板生成三个文件（或用项目工具窗口的生成器）。
2. 在目录类加一行 `const`；新增条目不需要改任何代码分支。
3. Inspector 路径：抽屉按 `/` 分层展示目录内容。
4. 代码里使用 `QuestId.From(QuestCatalog.MainIntro)`，或直接序列化一个 `QuestId` 字段。

## 规则

- **只扫描 `public const string`**。
- ID区分大小写。
- 重复路径在窗口里报 Error；运行时若自行建索引，应保留第一个。
- **路径发布后视为冻结**：改路径等于换 ID，所有已序列化引用会静默断开。成员名可以随意重命名。
- 序列化字段必须叫 `path`；抽屉找不到时会显示错误而不是静默退化成文本框。
- `struct` 不能继承，因此每个 ID 类型必须显式注册自己的抽屉（3 行）；目录类可以继承。

## 校验

菜单 `Tools/GrignardReagent/Layered Id Catalogs`：

- 按 `[LayeredIdCatalog]` 列出目录；
- 展开显示路径与成员名；
- 未打标的目录单独列出并告警；
- 空路径、重复路径、空目录显示为 Error。

## 测试

`Tests/EditMode` 随包；`Packages/manifest.json` 的 `testables` 已列出本包。在 Test Runner 里筛选 `GrignardReagent.LayeredId.Tests`。

## 发布到远端

当前是 repo 内嵌包，`git push` 由维护者决定。正式发布时：

1. 新建远端仓库，把 `Runtime/`、`Editor/`、`Tests/`、`package.json`、`README.md`、`CHANGELOG.md` 放到仓库根。
2. 打 tag `v0.1.0`。
3. 使用方的 `Packages/manifest.json` 改为 git URL + tag，并删除 `Packages/com.grignardreagent.layeredid`。
4. 程序集名不变，引用自动解析；若使用方把包加入 `testables`，测试继续可用。
