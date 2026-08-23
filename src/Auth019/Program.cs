using Auth019;
using Auth019.Data;
using Auth019.Middleware;
using Auth019.Models;
using Auth019.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("auth019db"));
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
            // Production keys come from the certificate store / mounted secrets.
            options.AddEncryptionCertificate(builder.Configuration["OpenIddict:EncryptionCertificateThumbprint"]!)
                   .AddSigningCertificate(builder.Configuration["OpenIddict:SigningCertificateThumbprint"]!);
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
