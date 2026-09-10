using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>Body of POST /api/courses/{id}/copy.</summary>
public class CourseCopyRequest
{
    /// <summary>簡介代碼 for the new course. Must not already be in use.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NewCourseId { get; set; } = string.Empty;
}
