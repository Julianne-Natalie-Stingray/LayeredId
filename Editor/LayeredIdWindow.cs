using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Layered Id 的唯一工具窗口: 生成脚本、维护 const 路径、校验目录.
/// 归属: LayeredId (Editor).
/// 不负责: asset 数据迁移; 删除 const 后旧引用需要手工处理.
/// </summary>
public sealed class LayeredIdWindow : EditorWindow
{
    private enum Tab
    {
        Generate,
        Consts,
        Validate
    }

    private const string MenuPath = "Tools/GrignardReagent/Layered Id";

    [SerializeField] private string domain = "NewDomain";
    [SerializeField] private string runtimeFolder = "Assets/IDs";
    [SerializeField] private string editorFolder = "Assets/Editor/IDs";
    [SerializeField] private string catalogPath = "";
    [SerializeField] private string newConstName = "NewEntry";
    [SerializeField] private string newConstPath = "NewDomain/NewEntry";

    private readonly HashSet<Type> expanded = new();
    private List<string> catalogSources;
    private Vector2 scroll;
    private Tab tab;

    [MenuItem(MenuPath)]
    private static void Open()
        => GetWindow<LayeredIdWindow>("Layered Id");

    private void OnGUI()
    {
        tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "Generate", "Consts", "Validate" });
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        switch (tab)
        {
            case Tab.Generate:
                DrawGenerateTab();
                break;
            case Tab.Consts:
                DrawConstsTab();
                break;
            case Tab.Validate:
                DrawValidateTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawGenerateTab()
    {
        EditorGUILayout.LabelField("Layered Identifier", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "生成 Id / Catalog / Drawer 三个脚本; const 的增删改在 Consts 页签.",
            MessageType.Info);

        domain = EditorGUILayout.TextField("Domain", domain);
        runtimeFolder = EditorGUILayout.TextField("Runtime Folder", runtimeFolder);
        editorFolder = EditorGUILayout.TextField("Editor Folder", editorFolder);

        using (new EditorGUI.DisabledScope(!IsValidIdentifier(domain)))
        {
            if (GUILayout.Button("Generate Scripts"))
            {
                GenerateScripts();
                GUIUtility.ExitGUI();
            }
        }

        if (!IsValidIdentifier(domain))
            EditorGUILayout.HelpBox("Domain 必须是合法标识符.", MessageType.Warning);

        EditorGUILayout.HelpBox(
            "Runtime 脚本所在程序集必须能引用 GrignardReagent.LayeredId"
            + "(Assets 根由 autoReferenced 自动获得); "
            + "Drawer 所在编辑器程序集必须引用 GrignardReagent.LayeredId.Editor.",
            MessageType.None);
    }

    private void DrawConstsTab()
    {
        EditorGUILayout.LabelField("Catalogs", EditorStyles.boldLabel);

        if (catalogSources == null)
            RefreshCatalogs();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                RefreshCatalogs();

            EditorGUILayout.LabelField(catalogSources.Count.ToString(), GUILayout.Width(40f));
        }

        foreach (string path in catalogSources)
        {
            bool selected = path == catalogPath;

            if (GUILayout.Button(
                    (selected ? "● " : "○ ") + path,
                    selected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                catalogPath = path;
            }
        }

        if (string.IsNullOrEmpty(catalogPath) || !File.Exists(catalogPath))
            return;

        DrawConsts(catalogPath);
    }

    private void DrawConsts(string path)
    {
        string text = File.ReadAllText(path);
        string catalogName = FindCatalogTypeName(text);

        if (catalogName == null)
        {
            EditorGUILayout.HelpBox("找不到 : LayeredIdCatalog 的类声明.", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Consts — {catalogName}", EditorStyles.boldLabel);

        List<IdConstEntry> entries = ReadConsts(text);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);

        foreach (IdConstEntry entry in entries)
        {
            names.Add(entry.Name);

            if (!string.IsNullOrWhiteSpace(entry.Path))
                paths.Add(entry.Path);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(entry.Name, GUILayout.Width(150f));
                string edited = EditorGUILayout.TextField(entry.Path);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(edited) || edited == entry.Path))
                {
                    if (GUILayout.Button("Update", GUILayout.Width(60f)))
                    {
                        UpdateConst(path, entry.Name, edited);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                {
                    RemoveConst(path, entry.Name);
                    GUIUtility.ExitGUI();
                }
            }
        }

        if (entries.Count == 0)
            EditorGUILayout.HelpBox("目录为空; 在下面添加第一条 const.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add", EditorStyles.boldLabel);

        newConstName = EditorGUILayout.TextField("Member", newConstName);
        newConstPath = EditorGUILayout.TextField("Path", newConstPath);

        bool validName = IsValidIdentifier(newConstName) && !names.Contains(newConstName);
        bool validPath = !string.IsNullOrWhiteSpace(newConstPath) && !paths.Contains(newConstPath);

        if (!validName)
            EditorGUILayout.HelpBox("成员名必须合法且唯一.", MessageType.Warning);
        if (!validPath)
            EditorGUILayout.HelpBox("路径必须非空且唯一.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!validName || !validPath))
        {
            if (GUILayout.Button("Add Const"))
            {
                AddConst(path, newConstName, newConstPath);
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawValidateTab()
    {
        var tagged = new List<Type>();
        var untagged = new List<Type>();

        foreach (Type type in TypeCache.GetTypesDerivedFrom<LayeredIdCatalog>())
        {
            if (!ShouldList(type))
                continue;

            bool hasAttribute = type.GetCustomAttributes(typeof(LayeredIdCatalogAttribute), false).Length > 0;
            (hasAttribute ? tagged : untagged).Add(type);
        }

        tagged.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        untagged.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Repaint();
        }

        EditorGUILayout.LabelField($"Tagged ({tagged.Count})", EditorStyles.boldLabel);

        foreach (Type type in tagged)
            DrawCatalog(type, false);

        if (untagged.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Untagged ({untagged.Count})", EditorStyles.boldLabel);

            foreach (Type type in untagged)
                DrawCatalog(type, true);
        }
    }

    private void DrawCatalog(Type catalogType, bool untagged)
    {
        bool isExpanded = expanded.Contains(catalogType);

        using (new EditorGUILayout.HorizontalScope())
        {
            bool next = EditorGUILayout.Foldout(
                isExpanded,
                $"{catalogType.Name} ({catalogType.Assembly.GetName().Name})",
                true);

            if (next != isExpanded)
            {
                if (next)
                    expanded.Add(catalogType);
                else
                    expanded.Remove(catalogType);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(46f)))
                EditorGUIUtility.systemCopyBuffer = catalogType.FullName;
        }

        if (untagged)
        {
            EditorGUILayout.HelpBox(
                $"未标记 {nameof(LayeredIdCatalogAttribute)}; 生成器与全工程校验不会发现它. ",
                MessageType.Warning);
        }

        if (!expanded.Contains(catalogType))
            return;

        IReadOnlyList<LayeredIdMember> members = LayeredIdCatalog.MembersOf(catalogType);

        using (new EditorGUI.IndentLevelScope())
        {
            foreach (LayeredIdMember member in members)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(member.Path) ? "<empty>" : member.Path,
                        GUILayout.MinWidth(180f));
                    EditorGUILayout.LabelField(member.Name, EditorStyles.miniLabel, GUILayout.MinWidth(120f));
                }
            }

            foreach (LayeredIdIssue issue in LayeredIdCatalog.Validate(catalogType))
            {
                string message = string.IsNullOrEmpty(issue.MemberName)
                    ? issue.Message
                    : $"{issue.MemberName}: {issue.Message}";

                EditorGUILayout.HelpBox(message, MessageType.Error);
            }
        }

        EditorGUILayout.Space();
    }

    private void GenerateScripts()
    {
        string name = domain.Trim();
        string runtime = runtimeFolder.Trim().TrimEnd('/');
        string editor = editorFolder.Trim().TrimEnd('/');

        if (!IsValidIdentifier(name))
            return;

        if (!EnsureFolder(runtime) || !EnsureFolder(editor))
            return;

        string idPath = $"{runtime}/{name}Id.cs";
        string catalogFile = $"{runtime}/{name}Catalog.cs";
        string drawerPath = $"{editor}/{name}IdDrawer.cs";

        bool exists = File.Exists(idPath) || File.Exists(catalogFile) || File.Exists(drawerPath);

        if (exists && !EditorUtility.DisplayDialog(
                "Layered Identifier",
                "目标脚本已存在, 覆盖?",
                "Overwrite",
                "Cancel"))
        {
            return;
        }

        File.WriteAllText(idPath, BuildIdSource(name));
        File.WriteAllText(catalogFile, BuildCatalogSource(name));
        File.WriteAllText(drawerPath, BuildDrawerSource(name));

        AssetDatabase.ImportAsset(idPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(catalogFile, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(drawerPath, ImportAssetOptions.ForceUpdate);

        catalogPath = catalogFile;
        catalogSources = null;
        tab = Tab.Consts;

        Debug.Log($"[LayeredId] 已生成 {idPath}, {catalogFile}, {drawerPath}.");
    }

    private void RefreshCatalogs()
    {
        catalogSources = new List<string>();

        foreach (string file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');

            if (normalized.IndexOf("/Tests/", StringComparison.Ordinal) >= 0)
                continue;

            string text = File.ReadAllText(file);

            if (Regex.IsMatch(text, @"class\s+[A-Za-z_]\w*\s*:\s*LayeredIdCatalog"))
                catalogSources.Add(normalized);
        }

        catalogSources.Sort(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(catalogPath) && !catalogSources.Contains(catalogPath))
            catalogPath = "";
    }

    private void AddConst(string path, string memberName, string idPath)
    {
        string text = File.ReadAllText(path);
        string catalogName = FindCatalogTypeName(text);

        if (catalogName == null)
        {
            Debug.LogWarning($"[LayeredId] 找不到 {path} 的目录类.");
            return;
        }

        int close = FindCatalogBodyClose(text, catalogName);

        if (close < 0)
        {
            Debug.LogWarning($"[LayeredId] 找不到 {path} 的类体.");
            return;
        }

        int lineStart = text.LastIndexOf('\n', close) + 1;
        string line = $"    public const string {memberName} = \"{EscapePath(idPath)}\";\n";

        WriteSource(path, text.Insert(lineStart, line));
    }

    private void UpdateConst(string path, string memberName, string idPath)
    {
        string text = File.ReadAllText(path);
        string pattern = $@"(?m)^(\s*public\s+const\s+string\s+{Regex.Escape(memberName)}\s*=\s*"")[^""]*(""\s*;)";
        string updated = Regex.Replace(
            text,
            pattern,
            match => match.Groups[1].Value + EscapePath(idPath) + match.Groups[2].Value);

        WriteSource(path, updated);
    }

    private void RemoveConst(string path, string memberName)
    {
        string text = File.ReadAllText(path);
        string pattern = $@"(?m)^\s*public\s+const\s+string\s+{Regex.Escape(memberName)}\s*=\s*""[^""]*""\s*;\s*\r?\n?";
        WriteSource(path, Regex.Replace(text, pattern, string.Empty));
    }

    private static void WriteSource(string path, string text)
    {
        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static List<IdConstEntry> ReadConsts(string text)
    {
        var entries = new List<IdConstEntry>();

        foreach (Match match in Regex.Matches(
                     text,
                     @"public\s+const\s+string\s+(?<name>[A-Za-z_]\w*)\s*=\s*""(?<path>[^""]*)""\s*;"))
        {
            entries.Add(new IdConstEntry
            {
                Name = match.Groups["name"].Value,
                Path = match.Groups["path"].Value,
            });
        }

        return entries;
    }

    private static string FindCatalogTypeName(string text)
    {
        Match match = Regex.Match(text, @"class\s+(?<name>[A-Za-z_]\w*)\s*:\s*LayeredIdCatalog");
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static int FindCatalogBodyClose(string text, string catalogName)
    {
        int classIndex = text.IndexOf("class " + catalogName, StringComparison.Ordinal);
        if (classIndex < 0)
            return -1;

        int open = text.IndexOf('{', classIndex);
        if (open < 0)
            return -1;

        int depth = 0;

        for (int index = open; index < text.Length; index++)
        {
            if (text[index] == '{')
                depth++;
            else if (text[index] == '}')
            {
                depth--;

                if (depth == 0)
                    return index;
            }
        }

        return -1;
    }

    private static string EscapePath(string path)
        => path.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string BuildIdSource(string name)
    {
        return "using System;\n"
            + "using UnityEngine;\n\n"
            + "/// <summary>\n"
            + $"/// {name} 的唯一标识: 只序列化一个层级路径字符串.\n"
            + $"/// 归属: {name}.\n"
            + "/// </summary>\n"
            + "[Serializable]\n"
            + $"public struct {name}Id : ILayeredId\n"
            + "{\n"
            + "    [SerializeField] private string path;\n\n"
            + "    public string Path => path ?? string.Empty;\n"
            + "    public bool IsValid => !string.IsNullOrWhiteSpace(path);\n\n"
            + $"    public static {name}Id None => default;\n"
            + $"    public static {name}Id From(string path) => new() {{ path = path ?? string.Empty }};\n\n"
            + $"    public bool Equals({name}Id other) => string.Equals(Path, other.Path, StringComparison.Ordinal);\n"
            + $"    public override bool Equals(object obj) => obj is {name}Id other && Equals(other);\n"
            + "    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Path);\n"
            + "    public override string ToString() => IsValid ? Path : \"None\";\n"
            + $"    public static bool operator ==({name}Id left, {name}Id right) => left.Equals(right);\n"
            + $"    public static bool operator !=({name}Id left, {name}Id right) => !left.Equals(right);\n"
            + "}\n";
    }

    private static string BuildCatalogSource(string name)
    {
        return "/// <summary>\n"
            + $"/// {name} 的路径目录.\n"
            + $"/// 归属: {name}.\n"
            + "/// </summary>\n"
            + "[LayeredIdCatalog]\n"
            + $"public sealed class {name}Catalog : LayeredIdCatalog\n"
            + "{\n"
            + $"    public const string Example = \"{name}/Example\";\n"
            + "}\n";
    }

    private static string BuildDrawerSource(string name)
    {
        return "using System;\n"
            + "using UnityEditor;\n\n"
            + "/// <summary>\n"
            + $"/// {name}Id 的 Inspector 绑定.\n"
            + $"/// 归属: {name}.\n"
            + "/// </summary>\n"
            + $"[CustomPropertyDrawer(typeof({name}Id))]\n"
            + $"public sealed class {name}IdDrawer : LayeredIdDrawer\n"
            + "{\n"
            + $"    protected override Type CatalogType => typeof({name}Catalog);\n"
            + "}\n";
    }

    /// <summary>只列生产代码里的公开顶层目录; 测试程序集与嵌套夹具不算目录.</summary>
    private static bool ShouldList(Type type)
    {
        if (type.IsAbstract || !type.IsVisible || type.IsNested)
            return false;

        string assemblyName = type.Assembly.GetName().Name;

        return assemblyName.IndexOf(".Tests", StringComparison.Ordinal) < 0
            && !assemblyName.EndsWith("Tests", StringComparison.Ordinal);
    }

    private static bool EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder == "Assets")
            return folder == "Assets";

        if (AssetDatabase.IsValidFolder(folder))
            return true;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);

        if (!EnsureFolder(parent))
            return false;

        string guid = AssetDatabase.CreateFolder(parent, leaf);
        return !string.IsNullOrEmpty(guid);
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        foreach (char character in name)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
                return false;
        }

        return true;
    }

    private sealed class IdConstEntry
    {
        public string Name;
        public string Path;
    }
}
