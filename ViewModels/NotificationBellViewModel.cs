using AMRSurveillance.Models;

namespace AMRSurveillance.ViewModels;

public class NotificationBellViewModel
{
    public List<Notification> Notifications
    {
        get;
        set;
    } = new();

    public int UnreadCount
    {
        get;
        set;
    }
}