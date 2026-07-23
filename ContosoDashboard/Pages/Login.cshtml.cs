using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using ContosoDashboard.Services;
using ContosoDashboard.Models;

namespace ContosoDashboard.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IUserService userService, ILogger<LoginModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public List<User>? Users { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            // Load all users for the dropdown
            Users = await _userService.GetAllUsersAsync();
        }

        public async Task<IActionResult> OnPostAsync(int selectedUserId)
        {
            _logger.LogDebug("Login POST: selectedUserId = {SelectedUserId}", selectedUserId);

            // Reload users for the form in case of error
            Users = await _userService.GetAllUsersAsync();

            if (selectedUserId == 0)
            {
                _logger.LogInformation("Login POST: no user selected");
                ErrorMessage = "Please select a user";
                return Page();
            }

            var user = await _userService.GetUserByIdAsync(selectedUserId);

            if (user == null)
            {
                _logger.LogWarning("Login POST: user {SelectedUserId} not found", selectedUserId);
                ErrorMessage = "User not found";
                return Page();
            }

            try
            {
                _logger.LogDebug("Login POST: attempting to sign in user {UserId}", user.UserId);

                // Create claims for the authenticated user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.DisplayName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("Login POST: sign in successful for user {UserId}", user.UserId);

                // Update last login date
                user.LastLoginDate = DateTime.UtcNow;
                await _userService.UpdateUserProfileAsync(user, user.UserId);

                // Redirect to home page
                return Redirect("/");
            }
            catch (Exception ex)
            {
                // Log the full exception but show a generic message to the user
                _logger.LogError(ex, "Login failed for user {UserId}", user.UserId);
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }
        }
    }
}
