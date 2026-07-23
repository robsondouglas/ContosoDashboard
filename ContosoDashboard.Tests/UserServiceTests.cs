using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class UserServiceTests
{
    private static (UserService service, ApplicationDbContext context) CreateService()
    {
        var context = TestDbContextFactory.Create();
        return (new UserService(context), context);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsUser_WhenExists()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "a@contoso.com", DisplayName = "A" });
        context.SaveChanges();

        var result = await service.GetUserByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("A", result!.DisplayName);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenMissing()
    {
        var (service, _) = CreateService();
        Assert.Null(await service.GetUserByIdAsync(999));
    }

    [Fact]
    public async Task GetUserByEmailAsync_IsCaseInsensitive()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "Person@Contoso.com", DisplayName = "P" });
        context.SaveChanges();

        var result = await service.GetUserByEmailAsync("person@contoso.COM");

        Assert.NotNull(result);
        Assert.Equal(1, result!.UserId);
    }

    [Fact]
    public async Task CreateOrUpdateUserAsync_CreatesNewUser_WithEmployeeRole()
    {
        var (service, context) = CreateService();

        var user = await service.CreateOrUpdateUserAsync("new@contoso.com", "New User");

        Assert.Equal(UserRole.Employee, user.Role);
        Assert.Equal(AvailabilityStatus.Available, user.AvailabilityStatus);
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task CreateOrUpdateUserAsync_UpdatesExistingUser_AndSetsLastLogin()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Old", LastLoginDate = null });
        context.SaveChanges();

        var user = await service.CreateOrUpdateUserAsync("e@contoso.com", "Updated");

        Assert.Equal("Updated", user.DisplayName);
        Assert.NotNull(user.LastLoginDate);
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_ReturnsFalse_WhenUserMissing()
    {
        var (service, _) = CreateService();

        Assert.False(await service.UpdateUserProfileAsync(new User { UserId = 999 }, 999));
    }

    [Fact]
    public async Task UpdateUserProfileAsync_ReturnsFalse_WhenNotOwnProfile()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Old" });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User { UserId = 1, DisplayName = "Hacked" }, 2);

        Assert.False(result);
        Assert.Equal("Old", context.Users.Single().DisplayName);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_UpdatesValidFields()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Old" });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User
        {
            UserId = 1,
            DisplayName = "New Name",
            PhoneNumber = "1234567890",
            Department = "Engineering",
            JobTitle = "Engineer",
            ProfilePhotoUrl = "https://example.com/photo.png",
            EmailNotificationsEnabled = false,
            InAppNotificationsEnabled = false
        }, 1);

        Assert.True(result);
        var u = context.Users.Single();
        Assert.Equal("New Name", u.DisplayName);
        Assert.Equal("1234567890", u.PhoneNumber);
        Assert.Equal("Engineering", u.Department);
        Assert.Equal("Engineer", u.JobTitle);
        Assert.Equal("https://example.com/photo.png", u.ProfilePhotoUrl);
        Assert.False(u.EmailNotificationsEnabled);
        Assert.False(u.InAppNotificationsEnabled);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_RejectsTooLongPhoneNumber()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Old", PhoneNumber = "555" });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User
        {
            UserId = 1,
            DisplayName = "Name",
            PhoneNumber = new string('9', 21)
        }, 1);

        Assert.True(result);
        // Too long -> not applied; existing value retained (not overwritten)
        Assert.Equal("555", context.Users.Single().PhoneNumber);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_ClearsOptionalFields_WhenBlank()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User
        {
            UserId = 1,
            Email = "e@contoso.com",
            DisplayName = "Old",
            PhoneNumber = "555",
            ProfilePhotoUrl = "https://old.com/x.png"
        });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User
        {
            UserId = 1,
            DisplayName = "Name",
            PhoneNumber = "",
            ProfilePhotoUrl = ""
        }, 1);

        Assert.True(result);
        var u = context.Users.Single();
        Assert.Null(u.PhoneNumber);
        Assert.Null(u.ProfilePhotoUrl);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_RejectsNonHttpProfilePhotoUrl()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Old" });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User
        {
            UserId = 1,
            DisplayName = "Name",
            ProfilePhotoUrl = "javascript:alert(1)"
        }, 1);

        Assert.True(result);
        Assert.Null(context.Users.Single().ProfilePhotoUrl);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_DoesNotOverwriteDisplayName_WhenBlank()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "Original" });
        context.SaveChanges();

        var result = await service.UpdateUserProfileAsync(new User { UserId = 1, DisplayName = "   " }, 1);

        Assert.True(result);
        Assert.Equal("Original", context.Users.Single().DisplayName);
    }

    [Fact]
    public async Task UpdateAvailabilityStatusAsync_ReturnsFalse_WhenUserMissing()
    {
        var (service, _) = CreateService();
        Assert.False(await service.UpdateAvailabilityStatusAsync(999, AvailabilityStatus.Busy));
    }

    [Fact]
    public async Task UpdateAvailabilityStatusAsync_UpdatesStatus()
    {
        var (service, context) = CreateService();
        context.Users.Add(new User { UserId = 1, Email = "e@contoso.com", DisplayName = "U", AvailabilityStatus = AvailabilityStatus.Available });
        context.SaveChanges();

        var result = await service.UpdateAvailabilityStatusAsync(1, AvailabilityStatus.OutOfOffice);

        Assert.True(result);
        Assert.Equal(AvailabilityStatus.OutOfOffice, context.Users.Single().AvailabilityStatus);
    }

    [Fact]
    public async Task GetTeamMembersAsync_ReturnsEmpty_WhenUserMissing()
    {
        var (service, _) = CreateService();
        Assert.Empty(await service.GetTeamMembersAsync(999));
    }

    [Fact]
    public async Task GetTeamMembersAsync_ReturnsSameDepartmentUsers_ExcludingSelf()
    {
        var (service, context) = CreateService();
        context.Users.AddRange(
            new User { UserId = 1, Email = "a@contoso.com", DisplayName = "Zoe", Department = "Eng" },
            new User { UserId = 2, Email = "b@contoso.com", DisplayName = "Amy", Department = "Eng" },
            new User { UserId = 3, Email = "c@contoso.com", DisplayName = "Bob", Department = "Eng" },
            new User { UserId = 4, Email = "d@contoso.com", DisplayName = "Dan", Department = "Sales" });
        context.SaveChanges();

        var result = await service.GetTeamMembersAsync(1);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, u => u.UserId == 1);
        Assert.DoesNotContain(result, u => u.Department == "Sales");
        // Ordered by display name
        Assert.Equal(new[] { "Amy", "Bob" }, result.Select(u => u.DisplayName).ToArray());
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsers_OrderedByDisplayName()
    {
        var (service, context) = CreateService();
        context.Users.AddRange(
            new User { UserId = 1, Email = "a@contoso.com", DisplayName = "Charlie" },
            new User { UserId = 2, Email = "b@contoso.com", DisplayName = "Alice" });
        context.SaveChanges();

        var result = await service.GetAllUsersAsync();

        Assert.Equal(new[] { "Alice", "Charlie" }, result.Select(u => u.DisplayName).ToArray());
    }
}
