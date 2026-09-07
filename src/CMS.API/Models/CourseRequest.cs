using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>課程 Course — write DTO for create and update.</summary>
public class CourseRequest
{
    /// <summary>
    /// 主代碼 — the primary key. The column is int IDENTITY, so this is ignored on create and
    /// carries the key from the body on update.
    /// </summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱</summary>
    [StringLength(300)]
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>原廠 — required FK.</summary>
    public short PartnerPkid { get; set; }

    /// <summary>課程群組 — nullable FK; null saves as NULL, not 0.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — required FK.</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>
    /// 下架日期. Deliberately not validated against ScheduleOn — the schema does not constrain the
    /// pair, and a validator would reject rows already present in the database.
    /// </summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數</summary>
    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    /// <summary>定價 — decimal(9,0).</summary>
    [Range(typeof(decimal), "0", "999999999")]
    public decimal ListPrice { get; set; }

    /// <summary>點數 — decimal(9,1).</summary>
    [Range(typeof(decimal), "0", "99999999.9")]
    public decimal LearningCredit { get; set; }

    /// <summary>教材</summary>
    [StringLength(500)]
    public string? Material { get; set; }

    /// <summary>課程目標</summary>
    [StringLength(4000)]
    public string? Objective { get; set; }

    /// <summary>適合對象</summary>
    [StringLength(500)]
    public string? Target { get; set; }

    /// <summary>先備知識</summary>
    [StringLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — nvarchar(max), so no length cap.</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 — nvarchar(max), so no length cap.</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註</summary>
    [StringLength(4000)]
    public string? Note { get; set; }

    /// <summary>其他資訊</summary>
    [StringLength(4000)]
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽</summary>
    public bool CanRepeat { get; set; }

    /// <summary>對應認證 — CourseInCertification is rewritten from this list on every save.</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>對應職務類別 — CourseJobCategories is rewritten from this list on every save.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
