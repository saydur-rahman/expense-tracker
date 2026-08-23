using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Api.Data;

public static class IdentitySeeder
{
    /// <summary>
    /// Ensures the app's roles exist, and promotes the configured seed email to Admin.
    /// Config-driven so no admin identity is committed to the repo and the same code
    /// works unchanged on the hosting environment.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var adminEmail = configuration["AdminSeed:Email"];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            logger.LogInformation(
                "Admin seed email {Email} has no account yet; register it and restart to grant the Admin role.",
                adminEmail);
            return;
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            logger.LogInformation("Granted the Admin role to {Email}.", adminEmail);
        }
    }
}
