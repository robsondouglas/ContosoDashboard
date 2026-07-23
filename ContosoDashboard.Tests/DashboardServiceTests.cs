using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using TaskStatus = ContosoDashboard.Models.TaskStatus;

namespace ContosoDashboard.Tests;

public class DashboardServiceTests
{
    private static (DashboardService service, ApplicationDbContext context) CreateService()
    {
        var context = TestDbContextFactory.Create();
        return (new DashboardService(context), context);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ComputesCountsForUser()
    {
        var (service, context) = CreateService();
        var today = DateTime.UtcNow;

        context.Tasks.AddRange(
            new TaskItem { TaskId = 1, Title = "A", AssignedUserId = 1, CreatedByUserId = 1, Status = TaskStatus.InProgress, DueDate = today },
            new TaskItem { TaskId = 2, Title = "B", AssignedUserId = 1, CreatedByUserId = 1, Status = TaskStatus.NotStarted, DueDate = today.AddDays(3) },
            new TaskItem { TaskId = 3, Title = "C", AssignedUserId = 1, CreatedByUserId = 1, Status = TaskStatus.Completed, DueDate = today },
            new TaskItem { TaskId = 4, Title = "D", AssignedUserId = 2, CreatedByUserId = 2, Status = TaskStatus.InProgress, DueDate = today });

        context.Projects.AddRange(
            new Project { ProjectId = 1, Name = "P1", ProjectManagerId = 1, Status = ProjectStatus.Active },
            new Project { ProjectId = 2, Name = "P2", ProjectManagerId = 1, Status = ProjectStatus.Completed },
            new Project
            {
                ProjectId = 3,
                Name = "P3",
                ProjectManagerId = 2,
                Status = ProjectStatus.Active,
                ProjectMembers = new List<ProjectMember> { new ProjectMember { ProjectMemberId = 1, ProjectId = 3, UserId = 1 } }
            });

        context.Notifications.AddRange(
            new Notification { NotificationId = 1, UserId = 1, Title = "n", Message = "m", Type = NotificationType.TaskUpdate, IsRead = false },
            new Notification { NotificationId = 2, UserId = 1, Title = "n", Message = "m", Type = NotificationType.TaskUpdate, IsRead = true },
            new Notification { NotificationId = 3, UserId = 2, Title = "n", Message = "m", Type = NotificationType.TaskUpdate, IsRead = false });
        context.SaveChanges();

        var summary = await service.GetDashboardSummaryAsync(1);

        Assert.Equal(2, summary.TotalActiveTasks); // tasks 1 and 2 (not completed, assigned to user 1)
        Assert.Equal(1, summary.TasksDueToday);    // task 1 (due today, not completed)
        Assert.Equal(2, summary.ActiveProjects);   // P1 (manager) + P3 (member); P2 completed excluded
        Assert.Equal(1, summary.UnreadNotifications);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsZeros_WhenNoData()
    {
        var (service, _) = CreateService();

        var summary = await service.GetDashboardSummaryAsync(1);

        Assert.Equal(0, summary.TotalActiveTasks);
        Assert.Equal(0, summary.TasksDueToday);
        Assert.Equal(0, summary.ActiveProjects);
        Assert.Equal(0, summary.UnreadNotifications);
    }

    [Fact]
    public async Task GetActiveAnnouncementsAsync_ReturnsOnlyActiveNonExpiredPublished_OrderedByPublishDateDesc()
    {
        var (service, context) = CreateService();
        var now = DateTime.UtcNow;

        context.Users.Add(new User { UserId = 1, Email = "a@contoso.com", DisplayName = "A" });
        context.Announcements.AddRange(
            new Announcement { AnnouncementId = 1, Title = "Active", Content = "c", CreatedByUserId = 1, IsActive = true, PublishDate = now.AddDays(-2) },
            new Announcement { AnnouncementId = 2, Title = "Newer", Content = "c", CreatedByUserId = 1, IsActive = true, PublishDate = now.AddDays(-1) },
            new Announcement { AnnouncementId = 3, Title = "Inactive", Content = "c", CreatedByUserId = 1, IsActive = false, PublishDate = now.AddDays(-1) },
            new Announcement { AnnouncementId = 4, Title = "Future", Content = "c", CreatedByUserId = 1, IsActive = true, PublishDate = now.AddDays(1) },
            new Announcement { AnnouncementId = 5, Title = "Expired", Content = "c", CreatedByUserId = 1, IsActive = true, PublishDate = now.AddDays(-3), ExpiryDate = now.AddDays(-1) });
        context.SaveChanges();

        var result = await service.GetActiveAnnouncementsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 2, 1 }, result.Select(a => a.AnnouncementId).ToArray());
    }

    [Fact]
    public async Task GetActiveAnnouncementsAsync_LimitsToFive()
    {
        var (service, context) = CreateService();
        var now = DateTime.UtcNow;
        context.Users.Add(new User { UserId = 1, Email = "a@contoso.com", DisplayName = "A" });
        for (int i = 1; i <= 8; i++)
            context.Announcements.Add(new Announcement { AnnouncementId = i, Title = $"A{i}", Content = "c", CreatedByUserId = 1, IsActive = true, PublishDate = now.AddDays(-i) });
        context.SaveChanges();

        var result = await service.GetActiveAnnouncementsAsync();

        Assert.Equal(5, result.Count);
    }
}
