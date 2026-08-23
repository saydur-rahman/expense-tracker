using ExpenseTracker019.Api;
using ExpenseTracker019.Api.Authorization;
using ExpenseTracker019.Api.Data;
using ExpenseTracker019.Api.Middleware;
using ExpenseTracker019.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("expensedb")));

// This service issues no tokens of its own. It is purely a resource server:
// it fetches Auth019's signing keys from the discovery document and validates
// the access tokens presented to it.
builder.Services
    .AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["Auth019:Issuer"]
            ?? throw new InvalidOperationException("Auth019:Issuer is not configured."));
        options.AddAudiences("expensetracker019-api");

        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ExpenseRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(Claims.Private.Scope, AppScopes.ExpenseRead);
    });

    options.AddPolicy(AuthPolicies.ExpenseWrite, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(Claims.Private.Scope, AppScopes.ExpenseWrite);
    });

    // Nothing here is anonymous; individual write actions opt up to ExpenseWrite.
    options.DefaultPolicy = options.GetPolicy(AuthPolicies.ExpenseRead)!;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<IMonthCycleService, MonthCycleService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Spa, policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<RequireWriteScopeFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Expense Tracker 019 API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "An access token issued by Auth019.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(CorsPolicies.Spa);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
