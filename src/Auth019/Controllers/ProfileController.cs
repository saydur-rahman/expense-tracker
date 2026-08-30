using Auth019.Dtos;
using Auth019.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Controllers;

/// <summary>
/// The signed-in user's own profile. Lives in Auth019 because Auth019 owns user
/// data — the expense API has no user table.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get()
    {
        var user = await CurrentUserAsync();
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    /// <summary>The countries the profile form offers, each with the currency it implies.</summary>
    [HttpGet("countries")]
    public ActionResult<IEnumerable<CountryOptionDto>> GetCountries()
        => Ok(Countries.All.Select(c => new CountryOptionDto
        {
            Code = c.Code,
            Name = c.Name,
            CurrencyCode = c.CurrencyCode,
        }));

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request)
    {
        if (ImpersonationBlocked() is { } blocked)
        {
            return blocked;
        }

        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!Countries.IsKnownCode(request.Country))
        {
            ModelState.AddModelError(nameof(request.Country), "Select your country.");
            return ValidationProblem(ModelState);
        }

        user.DisplayName = request.DisplayName.Trim();
        user.PhoneNumber = request.MobileNumber.Trim();
        user.Country = request.Country.ToUpperInvariant();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = string.Join(" ", result.Errors.Select(e => e.Description)),
            });
        }

        return Ok(ToDto(user));
    }

    /// <summary>
    /// Replaces the signed-in user's password. No current password is asked for — the
    /// bearer token is the proof of identity.
    /// </summary>
    /// <remarks>
    /// Only for an account that registered with a password. A Google-only account has
    /// none, and this is deliberately not a way to gain one: their credential lives at
    /// Google. Linking Google to an account that already has a password leaves the
    /// password in place, so those users keep this route.
    /// </remarks>
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (ImpersonationBlocked() is { } blocked)
        {
            return blocked;
        }

        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        // The profile screen hides this card for such an account; refusing here too keeps
        // the rule in one place rather than trusting the client to have hidden it.
        if (!await _userManager.HasPasswordAsync(user))
        {
            return PasswordProblem("You sign in with Google, so there is no password to change here.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return PasswordProblem("Enter a new password.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return PasswordProblem("The passwords do not match.");
        }

        // No current password is presented, so a reset token stands in for one — minted
        // and spent inside this request, never handed out. Strength stays Identity's to
        // judge, so a rejection reads the same as it would on the registration form.
        var result = await _userManager.ResetPasswordAsync(
            user,
            await _userManager.GeneratePasswordResetTokenAsync(user),
            request.NewPassword);

        return result.Succeeded
            ? NoContent()
            : PasswordProblem(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    private ActionResult? ImpersonationBlocked()
    {
        // An impersonated session is read-only everywhere else; editing someone's profile
        // or password while wearing their identity would be the loudest possible breach of that.
        if (string.IsNullOrEmpty(User.GetClaim(AppClaims.ImpersonatedBy)))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "This session is read-only. Exit impersonation to make changes.",
        });
    }

    private BadRequestObjectResult PasswordProblem(string message) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = message,
    });

    private Task<ApplicationUser?> CurrentUserAsync()
    {
        var id = User.GetClaim(Claims.Subject);
        return string.IsNullOrEmpty(id)
            ? Task.FromResult<ApplicationUser?>(null)
            : _userManager.FindByIdAsync(id)!;
    }

    private static ProfileDto ToDto(ApplicationUser user)
    {
        var country = Countries.Find(user.Country);
        return new ProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            MobileNumber = user.PhoneNumber,
            Country = user.Country,
            CountryName = country?.Name,
            CurrencyCode = country?.CurrencyCode,
            HasPassword = user.PasswordHash is not null,
        };
    }
}
