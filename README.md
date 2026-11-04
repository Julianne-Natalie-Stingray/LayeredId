# Layered Identifier

该包提供如下功能:

- 基于`const string`的分层仿`enum`. 

- `Inspector`内下拉菜单

- `Editor`图形化编辑界面

## 结构

```text
Runtime/   ILayeredId、LayeredIdCatalog、[LayeredIdCatalog]、LayeredIdMember、LayeredIdIssue
Editor/    LayeredIdDrawer、LayeredIdCatalogWindow
Tests/     EditMode：排序、Ordinal、空路径、重复、空目录
```

运行时程序集 **不引用 UnityEngine**，且 `autoReferenced = true`：项目内程序集可直接使用，无需显式引用。`Editor` 程序集同样 `autoReferenced = true`。

```csharp
// 示例: QuestId.cs
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
// 示例: QuestCatalog.cs
[LayeredIdCatalog]
public sealed class QuestCatalog : LayeredIdCatalog
{
    public const string MainIntro = "Quest/Main/Intro";
    public const string MainEnd   = "Quest/Main/End";
}
```

```csharp
// 示例: Editor/QuestIdDrawer.cs
[CustomPropertyDrawer(typeof(QuestId))]
internal sealed class QuestIdDrawer : LayeredIdDrawer
{
    protected override Type CatalogType => typeof(QuestCatalog);
}
```

asmdef：运行时已 `autoReferenced`，ID 所在程序集无需显式引用；

抽屉所在编辑器程序集需要引用 `GrignardReagent.LayeredId.Editor`。

## 使用

图形化界面在Unity上方工具栏`Tools/GrignardReagent/Layered Id`中

### 新建

以`QuestId`为例:

1. 在`Generate`页中添加新ID
2. `Domain`填ID类型名, 这里填`Quest`
3. `Runtime Folder`指定生成的运行时脚本路径, 需要在`Assets/`下. `QuestId`和`QuestIdCatalog`在此路径下
4. `Editor Folder`指定生成的编辑器脚本路径, 负责`Inspector`下拉菜单

代码里直接序列化一个 `QuestId` 字段，或使用 `QuestId.From(QuestCatalog.MainIntro)`。

### 增/删/改子标签

在`Const`页中进行. 

- 选中某一`LayeredId`类, 可以看到和删改该类所有成员. 

- 添加成员时, `Member`填入显示名, 命名必须在域内唯一.

- `Path`填入内部`const string`路径, `/`分隔路径, 指定目录树.

## 规则

- **只扫描 `public const string`**。
- ID区分大小写。
- 重复路径在窗口里报 Error；运行时若自行建索引，应保留第一个。
- `const string`路径一经发布即不可更改：改路径等于换 ID，所有已序列化引用会静默断开。
- 序列化字段必须叫 `path`；抽屉找不到时会显示错误。
- 每个 ID 类型必须显式注册自己的抽屉目录类可以继承。

## 校验

菜单 `Tools/GrignardReagent/Layered Id Catalogs`：

- 按 `[LayeredIdCatalog]` 列出目录；
- 展开显示路径与成员名；
- 未打标的目录单独列出并告警；
- 空路径、重复路径、空目录显示为 Error。

## 测试

`Tests/EditMode` 随包；`Packages/manifest.json` 的 `testables` 已列出本包。在 Test Runner 里筛选 `GrignardReagent.LayeredId.Tests`。
