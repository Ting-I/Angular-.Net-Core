namespace CMS.API.Models;

/// <summary>課程 Course — response model.</summary>
public class Course
{
    /// <summary>主代碼 — int IDENTITY primary key.</summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼</summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼</summary>
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址</summary>
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>原廠 — Partner_pkid, smallint NOT NULL.</summary>
    public short PartnerPkid { get; set; }

    /// <summary>課程群組 — CourseGroup_pkid, smallint NULL.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — PublishStatus_pkid, tinyint NOT NULL.</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期 — date column, mapped through DateOnlyTypeHandler.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期 — date column, mapped through DateOnlyTypeHandler.</summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數 — smallint.</summary>
    public short Hour { get; set; }

    /// <summary>定價 — decimal(9,0), whole dollars.</summary>
    public decimal ListPrice { get; set; }

    /// <summary>點數 — decimal(9,1), one decimal place.</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>教材</summary>
    public string? Material { get; set; }

    /// <summary>課程目標</summary>
    public string? Objective { get; set; }

    /// <summary>適合對象</summary>
    public string? Target { get; set; }

    /// <summary>先備知識</summary>
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — nvarchar(max).</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 — nvarchar(max).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註</summary>
    public string? Note { get; set; }

    /// <summary>其他資訊</summary>
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽</summary>
    public bool CanRepeat { get; set; }

    /// <summary>原廠 nav object — populated by the multi-map (INNER JOIN, never null in practice).</summary>
    public PartnerLookup? Partner { get; set; }

    /// <summary>課程群組 nav object — null whenever CourseGroupPkid is null (LEFT JOIN).</summary>
    public CourseGroupLookup? CourseGroup { get; set; }

    /// <summary>上架狀態 nav object — populated by the multi-map (INNER JOIN).</summary>
    public PublishStatusLookup? PublishStatus { get; set; }

    /// <summary>課程問答數 — count of CourseFAQ rows.</summary>
    public int CourseFaqCount { get; set; }

    /// <summary>相關連結數 — count of CourseRelatedLink rows.</summary>
    public int CourseRelatedLinkCount { get; set; }

    /// <summary>熱門課程數 — count of HotCourse rows.</summary>
    public int HotCourseCount { get; set; }

    /// <summary>
    /// 推薦課程數 — CourseRecomm rows on either side of the pair. The reference is by CourseId with
    /// no FOREIGN KEY behind it, so the database would let a delete orphan these rows.
    /// </summary>
    public int CourseRecommCount { get; set; }

    /// <summary>對應認證 — populated by GetByIdAsync only; always empty from GetAll / Query.</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>對應職務類別 — populated by GetByIdAsync only; always empty from GetAll / Query.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
