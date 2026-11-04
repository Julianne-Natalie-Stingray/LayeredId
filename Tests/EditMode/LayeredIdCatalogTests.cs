using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class LayeredIdCatalogTests
{
    [LayeredIdCatalog]
    private sealed class SampleCatalog : LayeredIdCatalog
    {
        public const string Music = "Ost/Main";
        public const string Mouse = "Sfx/UI/Mouse";
        public const string Empty = "";
        public static readonly string ReadOnly = "Sfx/ReadOnly";
        public string Instance = "Sfx/Instance";
    }

    private sealed class DuplicateCatalog : LayeredIdCatalog
    {
        public const string First = "Sfx/Dup";
        public const string Second = "Sfx/Dup";
    }

    private sealed class EmptyCatalog : LayeredIdCatalog
    {
    }

    [Test]
    public void All_ReturnsOnlyConstStringsSortedOrdinal()
    {
        IReadOnlyList<string> all = LayeredIdCatalog.AllOf(typeof(SampleCatalog));

        Assert.AreEqual(new[] { "Ost/Main", "Sfx/UI/Mouse" }, all.ToArray());
    }

    [Test]
    public void Contains_IsOrdinalCaseSensitive()
    {
        Assert.IsTrue(LayeredIdCatalog.Contains(typeof(SampleCatalog), "Sfx/UI/Mouse"));
        Assert.IsFalse(LayeredIdCatalog.Contains(typeof(SampleCatalog), "sfx/ui/mouse"));
        Assert.IsFalse(LayeredIdCatalog.Contains(typeof(SampleCatalog), " "));
    }

    [Test]
    public void Validate_ReportsEmptyPath()
    {
        IReadOnlyList<LayeredIdIssue> issues = LayeredIdCatalog.Validate(typeof(SampleCatalog));

        Assert.IsTrue(issues.Any(issue => issue.MemberName == nameof(SampleCatalog.Empty)));
    }

    [Test]
    public void Validate_ReportsDuplicatePathOnLaterMember()
    {
        IReadOnlyList<LayeredIdIssue> issues = LayeredIdCatalog.Validate(typeof(DuplicateCatalog));

        LayeredIdIssue duplicate = issues.Single(issue => issue.MemberName == nameof(DuplicateCatalog.Second));
        StringAssert.Contains(nameof(DuplicateCatalog.First), duplicate.Message);
    }

    [Test]
    public void Validate_ReportsEmptyCatalog()
    {
        IReadOnlyList<LayeredIdIssue> issues = LayeredIdCatalog.Validate(typeof(EmptyCatalog));

        Assert.AreEqual(1, issues.Count);
        Assert.IsEmpty(issues[0].MemberName);
    }
}

