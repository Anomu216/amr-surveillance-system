using System.ComponentModel.DataAnnotations;

namespace AMRSurveillance.Models;

public class Notification
{
    public int NotificationID { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserID { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NotificationType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Link { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}