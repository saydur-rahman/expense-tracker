using Auth019.Models;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth019.Data;

/// <summary>
/// Brings the authorization server to a usable state on startup: roles, OAuth scopes,
/// the SPA client registration, and the configured seed admin.
/// </summary>
public static class AuthSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await SeedRolesAsync(services);
        await SeedScopesAsync(services);
        await SeedSpaClientAsync(services, configuration, logger);
        await SeedAdminAsync(services, configuration, logger);
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private static async Task SeedScopesAsync(IServiceProvider services)
    {
        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();

        var descriptors = new[]
        {
            new OpenIddictScopeDescriptor
            {
                Name = AppScopes.ExpenseRead,
                DisplayName = "Read your expense data",
                Resources = { "expensetracker019-api" },
            },
            new OpenIddictScopeDescriptor
            {
                Name = AppScopes.ExpenseWrite,
                DisplayName = "Add and change your expense data",
                Resources = { "expensetracker019-api" },
            },
            new OpenIddictScopeDescriptor
            {
                Name = AppScopes.AuthAdmin,
                DisplayName = "Administer user accounts",
                Resources = { "auth019" },
            },
        };

        foreach (var descriptor in descriptors)
        {
            var existing = await scopeManager.FindByNameAsync(descriptor.Name!);
            if (existing is null)
            {
                await scopeManager.CreateAsync(descriptor);
            }
            else
            {
                await scopeManager.UpdateAsync(existing, descriptor);
            }
        }
    }

    private static async Task SeedSpaClientAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var appManager = services.GetRequiredService<IOpenIddictApplicationManager>();

        var spaOrigin = configuration["Spa:Origin"] ?? "http://localhost:5173";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AppClients.Spa,
            DisplayName = "Expense Tracker web app",
            // Public client: a browser app cannot keep a secret, so it authenticates
            // with PKCE instead of a client secret.
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            RedirectUris =
            {
                new Uri($"{spaOrigin}/callback"),
                new Uri($"{spaOrigin}/silent-renew"),
            },
            PostLogoutRedirectUris = { new Uri($"{spaOrigin}/") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                // Lets an admin exchange their token for a read-only impersonation token.
                Permissions.GrantTypes.TokenExchange,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + AppScopes.ExpenseRead,
                Permissions.Prefixes.Scope + AppScopes.ExpenseWrite,
                Permissions.Prefixes.Scope + AppScopes.AuthAdmin,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        var existing = await appManager.FindByClientIdAsync(AppClients.Spa);
        if (existing is null)
        {
            await appManager.CreateAsync(descriptor);
            logger.LogInformation("Registered SPA client for origin {Origin}.", spaOrigin);
        }
        else
        {
            await appManager.UpdateAsync(existing, descriptor);
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var adminEmail = configuration["AdminSeed:Email"];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            var password = configuration["AdminSeed:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation(
                    "Admin seed email {Email} has no account yet. Register it (or set AdminSeed:Password) and restart to grant the Admin role.",
                    adminEmail);
                return;
            }

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "Administrator",
            };

            var created = await userManager.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogWarning(
                    "Could not create seed admin {Email}: {Errors}",
                    adminEmail,
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(admin, AppRoles.User);
            logger.LogInformation("Created seed admin account {Email}.", adminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            logger.LogInformation("Granted the Admin role to {Email}.", adminEmail);
        }
    }
}
