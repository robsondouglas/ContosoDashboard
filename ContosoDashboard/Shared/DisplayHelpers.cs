using ContosoDashboard.Models;
using ModelTaskStatus = ContosoDashboard.Models.TaskStatus;

namespace ContosoDashboard.Shared;

/// <summary>
/// Presentation helpers shared across pages: avatar initials, relative time formatting,
/// and Bootstrap badge color/label mappings that were previously copied into each page.
/// </summary>
public static class DisplayHelpers
{
    /// <summary>
    /// Builds up to two uppercase initials from a display name.
    /// </summary>
    public static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    /// <summary>
    /// Formats a past UTC timestamp as a human-friendly relative time (e.g. "5 min ago").
    /// </summary>
    public static string GetRelativeTime(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} min ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} hours ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays} days ago";

        return dateTime.ToString("MMM dd, yyyy");
    }

    public static string GetPriorityColor(TaskPriority priority) => priority switch
    {
        TaskPriority.Critical => "danger",
        TaskPriority.High => "warning",
        TaskPriority.Medium => "info",
        TaskPriority.Low => "secondary",
        _ => "secondary"
    };

    public static string GetPriorityColor(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Urgent => "danger",
        NotificationPriority.Important => "warning",
        NotificationPriority.Informational => "info",
        _ => "secondary"
    };

    public static string GetStatusColor(ProjectStatus status) => status switch
    {
        ProjectStatus.Planning => "secondary",
        ProjectStatus.Active => "primary",
        ProjectStatus.OnHold => "warning",
        ProjectStatus.Completed => "success",
        _ => "secondary"
    };

    public static string GetStatusColor(AvailabilityStatus status) => status switch
    {
        AvailabilityStatus.Available => "success",
        AvailabilityStatus.Busy => "danger",
        AvailabilityStatus.InMeeting => "warning",
        AvailabilityStatus.OutOfOffice => "secondary",
        _ => "secondary"
    };

    public static string GetTaskStatusColor(ModelTaskStatus status) => status switch
    {
        ModelTaskStatus.NotStarted => "secondary",
        ModelTaskStatus.InProgress => "primary",
        ModelTaskStatus.Completed => "success",
        _ => "secondary"
    };

    public static string GetStatusText(AvailabilityStatus status) => status switch
    {
        AvailabilityStatus.Available => "Available",
        AvailabilityStatus.Busy => "Busy",
        AvailabilityStatus.InMeeting => "In Meeting",
        AvailabilityStatus.OutOfOffice => "Out of Office",
        _ => "Unknown"
    };
}
