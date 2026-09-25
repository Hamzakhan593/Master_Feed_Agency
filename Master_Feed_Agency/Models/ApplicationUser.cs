using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }
}
