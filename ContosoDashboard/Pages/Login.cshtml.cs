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
            // Reload users for the form in case of error
            Users = await _userService.GetAllUsersAsync();

            if (selectedUserId == 0)
            {
                ErrorMessage = "Please select a user";
                return Page();
            }

            var user = await _userService.GetUserByIdAsync(selectedUserId);

            if (user == null)
            {
                ErrorMessage = "User not found";
                return Page();
            }

            try
            {
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

                // Update last login date
                user.LastLoginDate = DateTime.UtcNow;
                await _userService.UpdateUserProfileAsync(user, user.UserId);

                // Redirect to home page
                return Redirect("/");
            }
            catch (Exception ex)
            {
                // Log the actual error server-side; show a generic message to the user
                _logger.LogError(ex, "Login failed for user {UserId}", selectedUserId);
                ErrorMessage = "Login failed. Please try again.";
                return Page();
            }
        }
    }
}
