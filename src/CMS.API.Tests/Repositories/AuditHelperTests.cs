using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// The reflection that decides what a RowAudit row says. Static and database-free, so it is tested
/// directly the way a BuildWhere is.
/// </summary>
public class AuditHelperTests
{
    /// <summary>Shaped like a real entity: pkid first, then the identifying string.</summary>
    private class Sample
    {
        public int Pkid { get; set; }

        public string CourseId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int Seats { get; set; }

        public DateOnly? ScheduleOn { get; set; }

        public List<int> CertificationPkids { get; set; } = [];
    }

    private class NoStrings
    {
        public int Pkid { get; set; }

        public int Seats { get; set; }
    }

    private class NoKey
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void AuditableProperties_AreInDeclarationOrder()
    {
        var names = AuditHelper.AuditableProperties(typeof(Sample)).Select(property => property.Name);

        Assert.Equal(
            new[] { "Pkid", "CourseId", "Title", "Seats", "ScheduleOn", "CertificationPkids" },
            names);
    }

    [Fact]
    public void FirstStringValue_TakesTheFirstStringPropertyNotTheFirstNonEmptyOne()
    {
        var entity = new Sample { CourseId = string.Empty, Title = "資料庫入門" };

        // Title is the more useful label, and that is exactly why the rule must not be "first
        // non-empty": the rule is positional, so a caller can predict it from the class.
        Assert.Equal(string.Empty, AuditHelper.FirstStringValue(entity));
    }

    [Fact]
    public void FirstStringValue_ReturnsTheValueOfTheFirstStringProperty()
    {
        Assert.Equal("C-001", AuditHelper.FirstStringValue(new Sample { CourseId = "C-001", Title = "x" }));
    }

    [Fact]
    public void FirstStringValue_WithNoStringProperty_IsNull()
    {
        Assert.Null(AuditHelper.FirstStringValue(new NoStrings { Pkid = 7 }));
    }

    [Fact]
    public void PrimaryKeyValue_ReadsPkidRegardlessOfCasing()
    {
        Assert.Equal("42", AuditHelper.PrimaryKeyValue(new Sample { Pkid = 42 }));
    }

    [Fact]
    public void PrimaryKeyValue_WithNoPkidProperty_IsEmpty()
    {
        // PrimaryKeyValues is NOT NULL, so the absence has to be a value.
        Assert.Equal(string.Empty, AuditHelper.PrimaryKeyValue(new NoKey { Name = "x" }));
    }

    [Fact]
    public void ChangedColumns_ListsOnlyTheChangedNamesInDeclarationOrder()
    {
        var before = new Sample { Pkid = 1, CourseId = "C-001", Title = "舊標題", Seats = 20 };
        var after = new Sample { Pkid = 1, CourseId = "C-001", Title = "新標題", Seats = 30 };

        Assert.Equal(new[] { "Title", "Seats" }, AuditHelper.ChangedColumns(before, after));
        Assert.Equal("Title,Seats", AuditHelper.ChangedColumnList(before, after));
    }

    [Fact]
    public void ChangedColumns_WithNothingChanged_IsEmpty()
    {
        var before = new Sample { Pkid = 1, CourseId = "C-001", Title = "標題", Seats = 20 };
        var after = new Sample { Pkid = 1, CourseId = "C-001", Title = "標題", Seats = 20 };

        Assert.Empty(AuditHelper.ChangedColumns(before, after));
        Assert.Equal(string.Empty, AuditHelper.ChangedColumnList(before, after));
    }

    [Fact]
    public void ChangedColumns_TreatsNullAsAValue()
    {
        var before = new Sample { ScheduleOn = null };
        var after = new Sample { ScheduleOn = new DateOnly(2026, 9, 8) };

        Assert.Equal(new[] { "ScheduleOn" }, AuditHelper.ChangedColumns(before, after));
        Assert.Equal(new[] { "ScheduleOn" }, AuditHelper.ChangedColumns(after, before));
    }

    [Fact]
    public void ChangedColumns_ComparesListsByTheirElements()
    {
        // An n-n key list is a fresh List<int> on every read; reference equality would report it
        // changed on every single save.
        var before = new Sample { CertificationPkids = [1, 2] };
        var after = new Sample { CertificationPkids = [1, 2] };

        Assert.Empty(AuditHelper.ChangedColumns(before, after));

        after.CertificationPkids = [1, 2, 3];
        Assert.Equal(new[] { "CertificationPkids" }, AuditHelper.ChangedColumns(before, after));
    }

    [Theory]
    [InlineData("abc", 10, "abc")]
    [InlineData("abcdef", 3, "abc")]
    [InlineData("abc", 3, "abc")]
    public void Truncate_CutsOnlyWhatIsTooLong(string value, int maxLength, string expected)
    {
        Assert.Equal(expected, AuditHelper.Truncate(value, maxLength));
    }

    [Fact]
    public void Truncate_PassesNullThrough()
    {
        Assert.Null(AuditHelper.Truncate(null, 10));
    }
}
