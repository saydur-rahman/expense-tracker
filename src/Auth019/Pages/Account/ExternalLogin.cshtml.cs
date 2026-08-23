using System.Security.Claims;
using Auth019.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth019.Pages.Account;

public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExternalLoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        ReturnUrl = returnUrl;

        if (remoteError is not null)
        {
            ErrorMessage = $"The external provider reported an error: {remoteError}";
            return Page();
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            ErrorMessage = "Could not read the external sign-in details.";
            return Page();
        }

        var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        if (user is null)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "The external provider did not supply an email address.";
                return Page();
            }

            // Only auto-link to an existing local account when the provider has
            // verified the address, so an unverified email can't claim someone's account.
            var emailVerified = string.Equals(
                info.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

            user = emailVerified ? await _userManager.FindByEmailAsync(email) : null;

            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = emailVerified,
                    DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                };

                var created = await _userManager.CreateAsync(user);
                if (!created.Succeeded)
                {
                    ErrorMessage = string.Join(" ", created.Errors.Select(e => e.Description));
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, AppRoles.User);
            }

            await _userManager.AddLoginAsync(user, info);
        }

        if (!user.IsActive)
        {
            ErrorMessage = "This account has been deactivated.";
            return Page();
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: true);

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : Redirect("~/");
    }
}
