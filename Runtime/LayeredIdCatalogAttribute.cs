using System;

/// <summary>
/// 标记目录类, 使生成器与全工程校验只扫描显式打标的类型.
/// 归属: LayeredId (Runtime).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class LayeredIdCatalogAttribute : Attribute
{
}

