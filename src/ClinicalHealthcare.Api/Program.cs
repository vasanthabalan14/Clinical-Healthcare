using System.Net.Mime;
using System.Text.Json;
using ClinicalHealthcare.Api.Abstractions;
using ClinicalHealthcare.Api.Infrastructure;
using ClinicalHealthcare.Infrastructure.Cache;
using ClinicalHealthcare.Infrastructure.Data;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Fail-fast: require both connection strings at startup (AC-001/AC-002) ──
static string RequireConnectionString(string envVar) =>
    Environment.GetEnvironmentVariable(envVar)
    ?? throw new InvalidOperationException(
        $"Required environment variable '{envVar}' is not set. " +
        $"Set it before starting the application.");

var sqlServerConnectionString = RequireConnectionString("SQLSERVER_CONNECTION_STRING");
var postgresConnectionString  = RequireConnectionString("POSTGRES_CONNECTION_STRING");
var redisConnectionString     = RequireConnectionString("REDIS_CONNECTION_STRING");

// ── ApplicationDbContext — SQL Server (AC-001) ────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(sqlServerConnectionString,
        sql => sql.MigrationsAssembly("ClinicalHealthcare.Infrastructure.SqlMigrations")));

// ── ClinicalDbContext — PostgreSQL (AC-002) ───────────────────────────────
builder.Services.AddDbContext<ClinicalDbContext>(options =>
    options.UseNpgsql(postgresConnectionString,
        npgsql => npgsql.MigrationsAssembly("ClinicalHealthcare.Infrastructure.PgMigrations")));

// ── Redis — IConnectionMultiplexer singleton (AC-003) ────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnectionString));

// ── Cache service (AC-003) ────────────────────────────────────────────────────
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection(CacheSettings.SectionName));
builder.Services.AddSingleton<ICacheService, CacheService>();

// ── Hangfire — background jobs (AC-004) ───────────────────────────────────────
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(sqlServerConnectionString));
builder.Services.AddHangfireServer();
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
{
    Attempts = 3,
    DelaysInSeconds = [30, 60, 120]
});

// ── Health checks (AC-001 / AC-005) ──────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(redisConnectionString, name: "redis", failureStatus: HealthStatus.Degraded);

// ── Swagger/OpenAPI — Development only (AC-002) ────────────────────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "ClinicalHealthcare API", Version = "v1" });
    });
}

// ── Vertical-slice endpoint registration (AC-005) ──────────────────────────
builder.Services.AddEndpointDefinitions(typeof(Program).Assembly, builder.Configuration);

// ── DI startup validation (AC-003) ────────────────────────────────────────
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes  = true;
});

// ── CORS — allow Angular dev origin ────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// ── Swagger UI — Development only (AC-002) ────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ClinicalHealthcare API v1"));
}

app.UseHttpsRedirection();
app.UseCors();

// ── Hangfire Dashboard — admin role only (AC-004) ────────────────────────────
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()]
});

// ── Health endpoint (AC-001) — returns JSON {"status":"Healthy"} ───────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status == HealthStatus.Healthy ? "Healthy"
                   : report.Status == HealthStatus.Degraded ? "Degraded"
                   : "Unhealthy"
        });
        await context.Response.WriteAsync(result);
    }
});

// ── Feature endpoints (AC-005) ────────────────────────────────────────────
app.MapEndpointDefinitions();

app.Run();
