using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ContosoDashboard.Services;

/// <summary>
/// Helpers for resolving the current application user id from authentication state,
/// centralizing the claim parsing that pages previously duplicated.
/// </summary>
public static class AuthenticationStateProviderExtensions
{
    /// <summary>
    /// Returns the current user's application id, or 0 when it cannot be determined.
    /// </summary>
    public static async Task<int> GetCurrentUserIdAsync(this AuthenticationStateProvider provider)
    {
        var authState = await provider.GetAuthenticationStateAsync();
        return authState.User.GetUserId();
    }

    /// <summary>
    /// Extracts the application user id from the NameIdentifier claim, or 0 when absent/invalid.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }

        return 0;
    }
}
