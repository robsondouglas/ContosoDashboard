using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Moq;
using TaskStatus = ContosoDashboard.Models.TaskStatus;

namespace ContosoDashboard.Tests;

public class TaskServiceTests
{
    private static (TaskService service, ApplicationDbContext context, Mock<INotificationService> notifications) CreateService()
    {
        var context = TestDbContextFactory.Create();
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateNotificationAsync(It.IsAny<Notification>()))
            .ReturnsAsync((Notification n) => n);
        var service = new TaskService(context, notifications.Object);
        return (service, context, notifications);
    }

    private static void SeedUsersAndProject(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { UserId = 1, Email = "creator@contoso.com", DisplayName = "Creator" },
            new User { UserId = 2, Email = "assignee@contoso.com", DisplayName = "Assignee" },
            new User { UserId = 3, Email = "manager@contoso.com", DisplayName = "Manager" },
            new User { UserId = 4, Email = "member@contoso.com", DisplayName = "Member" },
            new User { UserId = 5, Email = "outsider@contoso.com", DisplayName = "Outsider" });

        context.Projects.Add(new Project
        {
            ProjectId = 10,
            Name = "Proj",
            ProjectManagerId = 3,
            ProjectMembers = new List<ProjectMember>
            {
                new ProjectMember { ProjectMemberId = 100, ProjectId = 10, UserId = 4 }
            }
        });
        context.SaveChanges();
    }

    private static TaskItem MakeTask(int id, int assignee = 2, int creator = 1, int? projectId = 10,
        TaskPriority priority = TaskPriority.Medium, TaskStatus status = TaskStatus.NotStarted, DateTime? due = null)
        => new TaskItem
        {
            TaskId = id,
            Title = $"Task {id}",
            AssignedUserId = assignee,
            CreatedByUserId = creator,
            ProjectId = projectId,
            Priority = priority,
            Status = status,
            DueDate = due
        };

    [Fact]
    public async Task GetUserTasksAsync_ReturnsOnlyTasksAssignedToUser_OrderedByPriorityThenDueDate()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.AddRange(
            MakeTask(1, assignee: 2, priority: TaskPriority.Low, due: new DateTime(2030, 1, 5)),
            MakeTask(2, assignee: 2, priority: TaskPriority.Critical, due: new DateTime(2030, 1, 10)),
            MakeTask(3, assignee: 2, priority: TaskPriority.Critical, due: new DateTime(2030, 1, 1)),
            MakeTask(4, assignee: 5)); // different user
        context.SaveChanges();

        var result = await service.GetUserTasksAsync(2);

        Assert.Equal(3, result.Count);
        // Critical first, and within Critical, earlier due date first
        Assert.Equal(new[] { 3, 2, 1 }, result.Select(t => t.TaskId).ToArray());
    }

    [Fact]
    public async Task GetFilteredTasksAsync_NoFilters_ReturnsAllUserTasks()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.AddRange(MakeTask(1), MakeTask(2), MakeTask(3, assignee: 5));
        context.SaveChanges();

        var result = await service.GetFilteredTasksAsync(2, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFilteredTasksAsync_FiltersByStatusPriorityAndProject()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Projects.Add(new Project { ProjectId = 20, Name = "Other", ProjectManagerId = 3 });
        context.Tasks.AddRange(
            MakeTask(1, status: TaskStatus.InProgress, priority: TaskPriority.High, projectId: 10),
            MakeTask(2, status: TaskStatus.Completed, priority: TaskPriority.High, projectId: 10),
            MakeTask(3, status: TaskStatus.InProgress, priority: TaskPriority.Low, projectId: 10),
            MakeTask(4, status: TaskStatus.InProgress, priority: TaskPriority.High, projectId: 20));
        context.SaveChanges();

        var result = await service.GetFilteredTasksAsync(2, TaskStatus.InProgress, TaskPriority.High, 10);

        Assert.Single(result);
        Assert.Equal(1, result[0].TaskId);
    }

    [Fact]
    public async Task GetTaskByIdAsync_ReturnsNull_WhenTaskDoesNotExist()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);

        var result = await service.GetTaskByIdAsync(999, 1);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(2)] // assigned user
    [InlineData(1)] // creator
    [InlineData(3)] // project manager
    [InlineData(4)] // project member
    public async Task GetTaskByIdAsync_ReturnsTask_ForAuthorizedUsers(int requestingUserId)
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.SaveChanges();

        var result = await service.GetTaskByIdAsync(1, requestingUserId);

        Assert.NotNull(result);
        Assert.Equal(1, result!.TaskId);
    }

    [Fact]
    public async Task GetTaskByIdAsync_ReturnsNull_ForUnauthorizedUser()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.SaveChanges();

        var result = await service.GetTaskByIdAsync(1, 5); // outsider

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateTaskAsync_SetsTimestamps_PersistsTask_AndNotifiesAssignee()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);

        var created = await service.CreateTaskAsync(MakeTask(1, priority: TaskPriority.Critical));

        Assert.NotEqual(default, created.CreatedDate);
        Assert.NotEqual(default, created.UpdatedDate);
        Assert.Single(context.Tasks);
        notifications.Verify(n => n.CreateNotificationAsync(
            It.Is<Notification>(x => x.UserId == 2
                && x.Type == NotificationType.TaskAssignment
                && x.Priority == NotificationPriority.Urgent)),
            Times.Once);
    }

    [Fact]
    public async Task CreateTaskAsync_UsesImportantPriority_ForNonCriticalTask()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);

        await service.CreateTaskAsync(MakeTask(1, priority: TaskPriority.Low));

        notifications.Verify(n => n.CreateNotificationAsync(
            It.Is<Notification>(x => x.Priority == NotificationPriority.Important)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_ReturnsFalse_WhenTaskMissing()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);

        var result = await service.UpdateTaskStatusAsync(999, 1, TaskStatus.InProgress);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_ReturnsFalse_ForUnauthorizedUser()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.SaveChanges();

        var result = await service.UpdateTaskStatusAsync(1, 5, TaskStatus.InProgress);

        Assert.False(result);
        Assert.Equal(TaskStatus.NotStarted, context.Tasks.Single().Status);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_UpdatesStatus_ForAuthorizedUser()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.SaveChanges();

        var result = await service.UpdateTaskStatusAsync(1, 2, TaskStatus.InProgress);

        Assert.True(result);
        Assert.Equal(TaskStatus.InProgress, context.Tasks.Single().Status);
        // Not completed -> no completion notification
        notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_NotifiesCreator_WhenCompleted()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1, creator: 1));
        context.SaveChanges();

        var result = await service.UpdateTaskStatusAsync(1, 2, TaskStatus.Completed);

        Assert.True(result);
        notifications.Verify(n => n.CreateNotificationAsync(
            It.Is<Notification>(x => x.UserId == 1 && x.Type == NotificationType.TaskCompleted)),
            Times.Once);
    }

    [Fact]
    public async Task AddTaskCommentAsync_ReturnsFalse_WhenTaskMissing()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);

        var result = await service.AddTaskCommentAsync(999, 1, "hi");

        Assert.False(result);
    }

    [Fact]
    public async Task AddTaskCommentAsync_PersistsComment_AndNotifiesAssignee_WhenCommenterIsDifferent()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1, assignee: 2));
        context.SaveChanges();

        var result = await service.AddTaskCommentAsync(1, 1, "Great work");

        Assert.True(result);
        Assert.Single(context.TaskComments);
        notifications.Verify(n => n.CreateNotificationAsync(
            It.Is<Notification>(x => x.UserId == 2 && x.Type == NotificationType.TaskComment)),
            Times.Once);
    }

    [Fact]
    public async Task AddTaskCommentAsync_DoesNotNotify_WhenCommenterIsAssignee()
    {
        var (service, context, notifications) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1, assignee: 2));
        context.SaveChanges();

        var result = await service.AddTaskCommentAsync(1, 2, "self comment");

        Assert.True(result);
        notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task GetTaskCommentsAsync_ReturnsEmpty_WhenTaskMissing()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);

        var result = await service.GetTaskCommentsAsync(999, 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTaskCommentsAsync_ReturnsEmpty_ForUnauthorizedUser()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.TaskComments.Add(new TaskComment { CommentId = 1, TaskId = 1, UserId = 1, CommentText = "x" });
        context.SaveChanges();

        var result = await service.GetTaskCommentsAsync(1, 5); // outsider

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTaskCommentsAsync_ReturnsComments_OrderedByCreatedDate_ForAuthorizedUser()
    {
        var (service, context, _) = CreateService();
        SeedUsersAndProject(context);
        context.Tasks.Add(MakeTask(1));
        context.TaskComments.AddRange(
            new TaskComment { CommentId = 1, TaskId = 1, UserId = 1, CommentText = "second", CreatedDate = new DateTime(2030, 1, 2) },
            new TaskComment { CommentId = 2, TaskId = 1, UserId = 2, CommentText = "first", CreatedDate = new DateTime(2030, 1, 1) });
        context.SaveChanges();

        var result = await service.GetTaskCommentsAsync(1, 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[0].CommentText);
        Assert.Equal("second", result[1].CommentText);
    }
}
