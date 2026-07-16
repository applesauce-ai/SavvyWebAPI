using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Savvy.Api.Configuration;
using Savvy.Api.Health;
using Savvy.Api.Middleware;
using Savvy.Api.Security;
using Savvy.Application;
using Savvy.Application.Common;
using Savvy.Infrastructure;
using Savvy.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
// Add the secret store (real Azure Key Vault in Production, mock vault in dev).
// Added last so vault secrets override appsettings / user-secrets / env vars.
builder.Configuration.AddSavvyKeyVault(builder.Environment);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Enable "Authorize" with a bearer token in Swagger UI.
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

// Infrastructure (EF Core DbContext, persistence) + Application use-cases.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Current-user context from the request's ClaimsPrincipal.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

// JWT bearer authentication + authorization.
builder.Services.AddSavvyJwtAuth(builder.Configuration);

// Health checks: liveness (process up) + readiness (can reach the database).
// The "ready" tag lets us expose a DB-dependent readiness probe separately from liveness.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SavvyDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// --- Dev database bootstrap: apply migrations + seed demo data ---
// Production applies migrations via CI, not at app startup.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SavvyDbContext>();
    await db.Database.MigrateAsync();
    await SavvySeeder.SeedAsync(db);
}

// --- HTTP pipeline ---
// Catch-all exception -> ProblemDetails, wraps the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- Health endpoints (anonymous; used by the watchdog locally and Azure Monitor in prod) ---
// Liveness: is the process up and serving? (no dependency checks)
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
    ResponseWriter = HealthResponse.WriteAsync
}).AllowAnonymous();

// Readiness: can the app actually serve requests? (includes the database check)
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponse.WriteAsync
}).AllowAnonymous();

app.Run();

// Exposed for the integration-test WebApplicationFactory (Section 7).
public partial class Program { }
