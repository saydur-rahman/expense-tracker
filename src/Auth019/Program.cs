using Auth019;
using Auth019.Data;
using Auth019.Middleware;
using Auth019.Models;
using Auth019.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("auth019db"),
        // The history table moves into the schema too. Sharing one database with the
        // expense API means two migration histories, and a single default-named table
        // would have each service tearing down the other's migrations.
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema));
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

// OpenIddict looks these claims up by name when building tokens.
builder.Services.Configure<IdentityOptions>(options =>
{
    options.ClaimsIdentity.UserNameClaimType = Claims.Name;
    options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
    options.ClaimsIdentity.RoleClaimType = Claims.Role;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;

        // Google sends email_verified, but the handler does not map it by default —
        // without this it never reaches the principal, ExternalLogin reads it as
        // "unverified", and a Google address matching an existing account fails with
        // "email is already taken" instead of linking to it.
        options.ClaimActions.MapJsonKey("email_verified", "email_verified", ClaimValueTypes.Boolean);
    });
}

builder.Services
    .AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetEndSessionEndpointUris("connect/logout")
            .SetUserInfoEndpointUris("connect/userinfo")
            .SetRevocationEndpointUris("connect/revoke");

        options
            .AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            // RFC 8693. Used for admin impersonation: exchange an admin's token for a
            // read-only token acting as another user. See TokenExchangeHandler.
            .AllowTokenExchangeFlow();

        options.RegisterScopes(
            Scopes.OpenId,
            Scopes.Email,
            Scopes.Profile,
            Scopes.Roles,
            Scopes.OfflineAccess,
            AppScopes.ExpenseRead,
            AppScopes.ExpenseWrite,
            AppScopes.AuthAdmin);

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));

        // Pin the issuer when configured so it matches what the resource server
        // validates against, rather than being inferred from the request host.
        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            options.SetIssuer(issuer);
        }

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
        }
        else
        {
            var certificateBase64 = builder.Configuration["OpenIddict:SigningCertificateBase64"];
            var thumbprint = builder.Configuration["OpenIddict:SigningCertificateThumbprint"];

            if (!string.IsNullOrWhiteSpace(certificateBase64))
            {
                // Hosting that gives you no certificate store — App Service on the free
                // tier, most containers — can still supply a PFX as a base64 setting.
                var certificate = X509CertificateLoader.LoadPkcs12(
                    Convert.FromBase64String(certificateBase64),
                    builder.Configuration["OpenIddict:SigningCertificatePassword"],
                    X509KeyStorageFlags.EphemeralKeySet);

                options.AddSigningCertificate(certificate).AddEncryptionCertificate(certificate);
            }
            else if (!string.IsNullOrWhiteSpace(thumbprint))
            {
                options.AddEncryptionCertificate(builder.Configuration["OpenIddict:EncryptionCertificateThumbprint"]!)
                       .AddSigningCertificate(thumbprint);
            }
            else
            {
                // Last resort so a first deployment can be smoke-tested. These keys live
                // only in memory: every restart invalidates every token already issued,
                // signing everyone out. Supply a real certificate before real users arrive.
                Console.Error.WriteLine(
                    "WARNING: no OpenIddict signing certificate configured — falling back to " +
                    "ephemeral keys. Tokens will not survive a restart.");
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
            }
        }

        // The expense API validates tokens as plain JWTs, so they must not be encrypted.
        options.DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

        if (builder.Environment.IsDevelopment())
        {
            // Aspire wires the services together over plain HTTP locally. Never
            // relax this outside Development — tokens must not travel in the clear.
            aspNetCore.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        // Auth019 protects its own admin API with the tokens it issues.
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AuthAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppRoles.Admin);
        policy.RequireClaim(Claims.Private.Scope, AppScopes.AuthAdmin);
    });
});

builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<TokenExchangeHandler>();

builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Spa, policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Only wraps the JSON admin API; the OAuth endpoints and Razor pages produce
// their own protocol-correct error responses.
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseMiddleware<ExceptionHandlingMiddleware>());

app.UseStaticFiles();
app.UseRouting();

app.UseCors(CorsPolicies.Spa);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

// Auth019 is an identity server, not a destination — nothing lives at its root, so a
// 404 here was a dead end for anyone who landed on it. Several paths can: signing in
// from a login page opened directly (no ReturnUrl to come back to), the sign-out
// fallback, or simply typing the host. Send them all to the app instead.
app.MapGet("/", (IConfiguration configuration) =>
    Results.Redirect(configuration["Spa:Origin"] ?? "http://localhost:5173"))
    .ExcludeFromDescription();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.MigrateAsync();

    await AuthSeeder.SeedAsync(
        scope.ServiceProvider,
        app.Configuration,
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

app.Run();
