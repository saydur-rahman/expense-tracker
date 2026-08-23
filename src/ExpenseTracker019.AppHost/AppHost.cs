var builder = DistributedApplication.CreateBuilder(args);

// One SQL Server instance hosting two independent databases. The services never
// touch each other's schema — Auth019 owns identity, the API owns expense data.
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var authDb = sql.AddDatabase("auth019db");
var expenseDb = sql.AddDatabase("expensedb");

var spaOrigin = builder.AddParameter("spa-origin", "http://localhost:5173");

var auth = builder.AddProject<Projects.Auth019>("auth019")
    .WithReference(authDb)
    .WaitFor(authDb)
    .WithEnvironment("Spa__Origin", spaOrigin)
    .WithEnvironment("Cors__AllowedOrigins__0", spaOrigin)
    .WithExternalHttpEndpoints();

// Everyone must agree on one issuer string. Left to itself, Auth019 derives `iss`
// from the request host it sees, which is Aspire's proxy — while the API would be
// told a different internal address, and token validation fails on the mismatch.
// Pinning this one value for both (and the browser) keeps them consistent.
var authIssuer = auth.GetEndpoint("http");
auth.WithEnvironment("OpenIddict__Issuer", authIssuer);

var api = builder.AddProject<Projects.ExpenseTracker019_Api>("expenseapi")
    .WithReference(expenseDb)
    .WaitFor(expenseDb)
    .WithEnvironment("Auth019__Issuer", authIssuer)
    // Must not start before Auth019 is serving its discovery document.
    .WithReference(auth)
    .WaitFor(auth)
    .WithEnvironment("Cors__AllowedOrigins__0", spaOrigin)
    .WithExternalHttpEndpoints();

builder.AddNpmApp("web", "../frontend", "dev")
    .WithReference(api)
    .WithReference(auth)
    .WaitFor(api)
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WithEnvironment("VITE_AUTH_BASE_URL", auth.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT", port: 5173)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
