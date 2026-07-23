using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

/// <summary>
/// Shared authorization rules for domain entities, so access checks are defined once
/// instead of being duplicated across service methods.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// A user may access a task if they are its assignee, its creator, the manager of
    /// its project, or a member of its project.
    /// </summary>
    public static bool CanBeAccessedBy(this TaskItem task, int userId)
    {
        var isAssignedUser = task.AssignedUserId == userId;
        var isCreator = task.CreatedByUserId == userId;
        var isProjectMember = task.Project?.ProjectMembers.Any(pm => pm.UserId == userId) ?? false;
        var isProjectManager = task.Project?.ProjectManagerId == userId;

        return isAssignedUser || isCreator || isProjectMember || isProjectManager;
    }

    /// <summary>
    /// A user may access a project if they are its manager or one of its members.
    /// </summary>
    public static bool CanBeAccessedBy(this Project project, int userId)
    {
        var isProjectManager = project.ProjectManagerId == userId;
        var isProjectMember = project.ProjectMembers.Any(pm => pm.UserId == userId);

        return isProjectManager || isProjectMember;
    }
}
