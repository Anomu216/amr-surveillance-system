using System.ComponentModel.DataAnnotations;
using AMRSurveillance.Security;

namespace AMRSurveillance.Models;

public class AuditLog
{
    [Key]
    public long AuditLogID { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? EntityKey { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTime TimestampEat { get; set; } = EastAfricaTime.Now;
}