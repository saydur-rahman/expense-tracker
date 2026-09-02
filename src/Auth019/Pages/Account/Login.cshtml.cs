using System.ComponentModel.DataAnnotations;
using Auth019.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth019.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public bool HasGoogleLogin { get; private set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        HasGoogleLogin = await HasGoogleProviderAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasGoogleLogin = await HasGoogleProviderAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        if (!user.IsActive)
        {
            ErrorMessage = "This account has been deactivated.";
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, Input.Password, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ErrorMessage = result.IsLockedOut
                ? "This account is temporarily locked. Try again later."
                : "Invalid email or password.";
            return Page();
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return RedirectToSafeReturnUrl();
    }

    public async Task<IActionResult> OnPostExternalLoginAsync(string provider)
    {
        if (!await HasGoogleProviderAsync())
        {
            return BadRequest("External login is not configured.");
        }

        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { ReturnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

        // Always ask Google which account to use. Without this it silently reuses whichever
        // account the browser is already signed in to, and there is then no way to sign in
        // as anyone else — or to pick a different one after a failed attempt, which is when
        // you need it most.
        //
        // This was tried once before as a fix for logout "coming back as the same session",
        // and removed on the grounds that Google shows its own chooser anyway. It does not:
        // with a single signed-in account it goes straight through, silently. Removing it
        // was right for the reason it was added and wrong for this one.
        //
        // The cost is one extra click for someone with a single Google account. That is the
        // trade being made deliberately.
        properties.SetParameter(GoogleChallengeProperties.PromptParameterKey, "select_account");

        return Challenge(properties, provider);
    }

    private async Task<bool> HasGoogleProviderAsync()
        => (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .Any(s => s.Name == "Google");

    private IActionResult RedirectToSafeReturnUrl()
        => Url.IsLocalUrl(ReturnUrl) ? Redirect(ReturnUrl!) : Redirect("~/");
}
