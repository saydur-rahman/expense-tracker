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
        // An impersonated session is read-only everywhere else; editing someone's
        // profile while wearing their identity would be the loudest possible breach of that.
        if (!string.IsNullOrEmpty(User.GetClaim(AppClaims.ImpersonatedBy)))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "This session is read-only. Exit impersonation to make changes.",
            });
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
        };
    }
}
