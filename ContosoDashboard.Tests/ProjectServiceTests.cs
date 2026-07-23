using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class ProjectServiceTests
{
    private static (ProjectService service, ApplicationDbContext context) CreateService()
    {
        var context = TestDbContextFactory.Create();
        return (new ProjectService(context), context);
    }

    private static void SeedUsers(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { UserId = 1, Email = "pm@contoso.com", DisplayName = "PM" },
            new User { UserId = 2, Email = "member@contoso.com", DisplayName = "Member" },
            new User { UserId = 3, Email = "outsider@contoso.com", DisplayName = "Outsider" });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetUserProjectsAsync_ReturnsManagedAndMemberProjects_WithoutDuplicates()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.AddRange(
            new Project { ProjectId = 1, Name = "Managed", ProjectManagerId = 1, CreatedDate = new DateTime(2030, 1, 1) },
            new Project
            {
                ProjectId = 2,
                Name = "MemberOf",
                ProjectManagerId = 3,
                CreatedDate = new DateTime(2030, 2, 1),
                ProjectMembers = new List<ProjectMember> { new ProjectMember { ProjectMemberId = 1, ProjectId = 2, UserId = 1 } }
            },
            new Project { ProjectId = 3, Name = "Unrelated", ProjectManagerId = 3, CreatedDate = new DateTime(2030, 3, 1) });
        context.SaveChanges();

        var result = await service.GetUserProjectsAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.ProjectId == 1);
        Assert.Contains(result, p => p.ProjectId == 2);
        // Ordered by CreatedDate descending
        Assert.True(result[0].CreatedDate >= result[1].CreatedDate);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsNull_WhenMissing()
    {
        var (service, context) = CreateService();
        SeedUsers(context);

        Assert.Null(await service.GetProjectByIdAsync(999, 1));
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsProject_ForManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.SaveChanges();

        var result = await service.GetProjectByIdAsync(1, 1);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsProject_ForMember()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project
        {
            ProjectId = 1,
            Name = "P",
            ProjectManagerId = 1,
            ProjectMembers = new List<ProjectMember> { new ProjectMember { ProjectMemberId = 1, ProjectId = 1, UserId = 2 } }
        });
        context.SaveChanges();

        var result = await service.GetProjectByIdAsync(1, 2);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsNull_ForUnauthorizedUser()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.SaveChanges();

        Assert.Null(await service.GetProjectByIdAsync(1, 3));
    }

    [Fact]
    public async Task CreateProjectAsync_SetsTimestamps_AndPersists()
    {
        var (service, context) = CreateService();
        SeedUsers(context);

        var created = await service.CreateProjectAsync(new Project { Name = "New", ProjectManagerId = 1 });

        Assert.NotEqual(default, created.CreatedDate);
        Assert.NotEqual(default, created.UpdatedDate);
        Assert.Single(context.Projects);
    }

    [Fact]
    public async Task UpdateProjectAsync_ReturnsFalse_WhenMissing()
    {
        var (service, context) = CreateService();
        SeedUsers(context);

        var result = await service.UpdateProjectAsync(new Project { ProjectId = 999, ProjectManagerId = 1 }, 1);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateProjectAsync_ReturnsFalse_WhenNotManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "Old", ProjectManagerId = 1 });
        context.SaveChanges();

        var result = await service.UpdateProjectAsync(
            new Project { ProjectId = 1, Name = "Hacked", ProjectManagerId = 1 }, 3);

        Assert.False(result);
        Assert.Equal("Old", context.Projects.Single().Name);
    }

    [Fact]
    public async Task UpdateProjectAsync_UpdatesFields_ForManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "Old", ProjectManagerId = 1, Status = ProjectStatus.Planning });
        context.SaveChanges();

        var result = await service.UpdateProjectAsync(new Project
        {
            ProjectId = 1,
            Name = "New Name",
            Description = "New Desc",
            Status = ProjectStatus.Active,
            TargetCompletionDate = new DateTime(2031, 1, 1)
        }, 1);

        Assert.True(result);
        var updated = context.Projects.Single();
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("New Desc", updated.Description);
        Assert.Equal(ProjectStatus.Active, updated.Status);
        Assert.Equal(new DateTime(2031, 1, 1), updated.TargetCompletionDate);
    }

    [Fact]
    public async Task AddProjectMemberAsync_ReturnsFalse_WhenProjectMissing()
    {
        var (service, context) = CreateService();
        SeedUsers(context);

        Assert.False(await service.AddProjectMemberAsync(999, 2, "Dev", 1));
    }

    [Fact]
    public async Task AddProjectMemberAsync_ReturnsFalse_WhenNotManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.SaveChanges();

        Assert.False(await service.AddProjectMemberAsync(1, 2, "Dev", 3));
        Assert.Empty(context.ProjectMembers);
    }

    [Fact]
    public async Task AddProjectMemberAsync_ReturnsFalse_WhenAlreadyMember()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 1, UserId = 2 });
        context.SaveChanges();

        Assert.False(await service.AddProjectMemberAsync(1, 2, "Dev", 1));
        Assert.Single(context.ProjectMembers);
    }

    [Fact]
    public async Task AddProjectMemberAsync_AddsMember_ForManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.SaveChanges();

        var result = await service.AddProjectMemberAsync(1, 2, "Developer", 1);

        Assert.True(result);
        var member = context.ProjectMembers.Single();
        Assert.Equal(2, member.UserId);
        Assert.Equal("Developer", member.Role);
    }

    [Fact]
    public async Task GetProjectMembersAsync_ReturnsEmpty_WhenProjectMissing()
    {
        var (service, context) = CreateService();
        SeedUsers(context);

        Assert.Empty(await service.GetProjectMembersAsync(999, 1));
    }

    [Fact]
    public async Task GetProjectMembersAsync_ReturnsEmpty_ForUnauthorizedUser()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 1, UserId = 2 });
        context.SaveChanges();

        Assert.Empty(await service.GetProjectMembersAsync(1, 3));
    }

    [Fact]
    public async Task GetProjectMembersAsync_ReturnsMembers_ForManager()
    {
        var (service, context) = CreateService();
        SeedUsers(context);
        context.Projects.Add(new Project { ProjectId = 1, Name = "P", ProjectManagerId = 1 });
        context.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 1, UserId = 2 });
        context.SaveChanges();

        var result = await service.GetProjectMembersAsync(1, 1);

        Assert.Single(result);
        Assert.Equal(2, result[0].UserId);
    }
}
