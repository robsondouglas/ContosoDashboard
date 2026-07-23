using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class NotificationServiceTests
{
    private static (NotificationService service, ApplicationDbContext context) CreateService()
    {
        var context = TestDbContextFactory.Create();
        return (new NotificationService(context), context);
    }

    private static Notification MakeNotification(int id, int userId, bool isRead = false,
        NotificationPriority priority = NotificationPriority.Informational, DateTime? created = null)
        => new Notification
        {
            NotificationId = id,
            UserId = userId,
            Title = $"N{id}",
            Message = "msg",
            Type = NotificationType.TaskUpdate,
            Priority = priority,
            IsRead = isRead,
            CreatedDate = created ?? DateTime.UtcNow
        };

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsOnlyUsersNotifications()
    {
        var (service, context) = CreateService();
        context.Notifications.AddRange(
            MakeNotification(1, 1),
            MakeNotification(2, 1),
            MakeNotification(3, 2));
        context.SaveChanges();

        var result = await service.GetUserNotificationsAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Equal(1, n.UserId));
    }

    [Fact]
    public async Task GetUserNotificationsAsync_UnreadOnly_FiltersReadNotifications()
    {
        var (service, context) = CreateService();
        context.Notifications.AddRange(
            MakeNotification(1, 1, isRead: false),
            MakeNotification(2, 1, isRead: true));
        context.SaveChanges();

        var result = await service.GetUserNotificationsAsync(1, unreadOnly: true);

        Assert.Single(result);
        Assert.Equal(1, result[0].NotificationId);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_OrdersByPriorityThenCreatedDateDescending()
    {
        var (service, context) = CreateService();
        context.Notifications.AddRange(
            MakeNotification(1, 1, priority: NotificationPriority.Informational, created: new DateTime(2030, 1, 1)),
            MakeNotification(2, 1, priority: NotificationPriority.Urgent, created: new DateTime(2030, 1, 1)),
            MakeNotification(3, 1, priority: NotificationPriority.Urgent, created: new DateTime(2030, 2, 1)));
        context.SaveChanges();

        var result = await service.GetUserNotificationsAsync(1);

        // Priority enum: Urgent=0, Important=1, Informational=2. OrderByDescending(Priority)
        // means Informational (2) sorts first, then Urgent by newest date.
        Assert.Equal(1, result[0].NotificationId);
        Assert.Equal(3, result[1].NotificationId);
        Assert.Equal(2, result[2].NotificationId);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_LimitsToFifty()
    {
        var (service, context) = CreateService();
        for (int i = 1; i <= 60; i++)
            context.Notifications.Add(MakeNotification(i, 1));
        context.SaveChanges();

        var result = await service.GetUserNotificationsAsync(1);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public async Task CreateNotificationAsync_SetsCreatedDateAndUnread_AndPersists()
    {
        var (service, context) = CreateService();

        var notification = await service.CreateNotificationAsync(new Notification
        {
            UserId = 1,
            Title = "Hello",
            Message = "World",
            Type = NotificationType.SystemAnnouncement,
            IsRead = true // should be reset to false
        });

        Assert.False(notification.IsRead);
        Assert.NotEqual(default, notification.CreatedDate);
        Assert.Single(context.Notifications);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_WhenMissing()
    {
        var (service, _) = CreateService();
        Assert.False(await service.MarkAsReadAsync(999, 1));
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_ForOtherUsersNotification()
    {
        var (service, context) = CreateService();
        context.Notifications.Add(MakeNotification(1, userId: 1, isRead: false));
        context.SaveChanges();

        var result = await service.MarkAsReadAsync(1, 2);

        Assert.False(result);
        Assert.False(context.Notifications.Single().IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_MarksOwnNotificationRead()
    {
        var (service, context) = CreateService();
        context.Notifications.Add(MakeNotification(1, userId: 1, isRead: false));
        context.SaveChanges();

        var result = await service.MarkAsReadAsync(1, 1);

        Assert.True(result);
        Assert.True(context.Notifications.Single().IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadForUser()
    {
        var (service, context) = CreateService();
        context.Notifications.AddRange(
            MakeNotification(1, userId: 1, isRead: false),
            MakeNotification(2, userId: 1, isRead: false),
            MakeNotification(3, userId: 1, isRead: true),
            MakeNotification(4, userId: 2, isRead: false));
        context.SaveChanges();

        var count = await service.GetUnreadCountAsync(1);

        Assert.Equal(2, count);
    }
}
