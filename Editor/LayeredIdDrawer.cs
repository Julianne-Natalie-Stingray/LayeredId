using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>
/// 分层标识的通用抽屉: 读取名为 path 的子字段, 以目录路径提供分层下拉.
/// 归属: LayeredId (Editor).
/// 不负责: 目录内容与 ID 类型的相等性.
/// </summary>
public abstract class LayeredIdDrawer : PropertyDrawer
{
    private const string PathFieldName = "path";
    private const float MissingHeight = 2f;

    private static readonly Dictionary<Type, AdvancedDropdownState> States = new();

    /// <summary>该 ID 类型使用的目录类型; 由派生抽屉固定.</summary>
    protected abstract Type CatalogType { get; }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty pathProperty = property.FindPropertyRelative(PathFieldName);

        if (pathProperty == null || pathProperty.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(
                position,
                $"{property.displayName} 缺少 string 子字段 {PathFieldName}; 检查 ID 结构体的序列化形状. ",
                MessageType.Error);

            return;
        }

        if (CatalogType == null)
        {
            EditorGUI.HelpBox(position, "派生抽屉未指定 CatalogType. ", MessageType.Error);
            return;
        }

        float fieldHeight = EditorGUIUtility.singleLineHeight;
        var fieldRect = new Rect(position.x, position.y, position.width, fieldHeight);

        string current = pathProperty.stringValue;
        bool missing = IsMissing(current);
        var content = new GUIContent(
            string.IsNullOrWhiteSpace(current) ? "None" : current,
            null,
            missing ? "路径不在目录中. " : current);

        EditorGUI.BeginProperty(position, label, property);

        Rect buttonRect = EditorGUI.PrefixLabel(fieldRect, label);

        if (EditorGUI.DropdownButton(buttonRect, content, FocusType.Keyboard))
        {
            var dropdown = new PathDropdown(
                CatalogType,
                StateOf(CatalogType),
                path =>
                {
                    pathProperty.stringValue = path;
                    pathProperty.serializedObject.ApplyModifiedProperties();
                });

            dropdown.Show(buttonRect);
        }

        EditorGUI.EndProperty();

        if (missing)
        {
            var line = new Rect(position.x, position.yMax - 1f, position.width, 1f);
            EditorGUI.DrawRect(line, new Color(0.9f, 0.2f, 0.2f));
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty pathProperty = property.FindPropertyRelative(PathFieldName);
        bool missing = pathProperty != null
            && pathProperty.propertyType == SerializedPropertyType.String
            && IsMissing(pathProperty.stringValue);

        return EditorGUIUtility.singleLineHeight + (missing ? MissingHeight : 0f);
    }

    private bool IsMissing(string path)
        => CatalogType != null
            && !string.IsNullOrWhiteSpace(path)
            && !LayeredIdCatalog.Contains(CatalogType, path);

    private static AdvancedDropdownState StateOf(Type catalogType)
    {
        if (!States.TryGetValue(catalogType, out AdvancedDropdownState state))
        {
            state = new AdvancedDropdownState();
            States.Add(catalogType, state);
        }

        return state;
    }

    /// <summary>按 '/' 拆分的下拉; 同一父节点下的子节点保持目录的 Ordinal 排序.</summary>
    private sealed class PathDropdown : AdvancedDropdown
    {
        private readonly Type catalogType;
        private readonly Action<string> onSelected;
        private readonly Dictionary<AdvancedDropdownItem, string> paths = new();

        internal PathDropdown(Type catalogType, AdvancedDropdownState state, Action<string> onSelected)
            : base(state)
        {
            this.catalogType = catalogType;
            this.onSelected = onSelected;
            minimumSize = new Vector2(240f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Layered Id");
            var none = new AdvancedDropdownItem("None");
            paths.Add(none, string.Empty);
            root.AddChild(none);

            var nodes = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);

            foreach (string path in LayeredIdCatalog.AllOf(catalogType))
            {
                string[] segments = path.Split('/');
                AdvancedDropdownItem parent = root;
                string prefix = string.Empty;

                for (int index = 0; index < segments.Length; index++)
                {
                    prefix = index == 0 ? segments[index] : prefix + "/" + segments[index];

                    if (!nodes.TryGetValue(prefix, out AdvancedDropdownItem node))
                    {
                        node = new AdvancedDropdownItem(segments[index]);
                        nodes.Add(prefix, node);
                        parent.AddChild(node);
                    }

                    parent = node;
                }

                paths[parent] = path;
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (paths.TryGetValue(item, out string path))
                onSelected(path);
        }
    }
}

