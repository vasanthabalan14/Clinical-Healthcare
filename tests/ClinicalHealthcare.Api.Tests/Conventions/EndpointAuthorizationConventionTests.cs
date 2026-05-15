using ClinicalHealthcare.Api.Abstractions;
using ClinicalHealthcare.Infrastructure.Data;
using ClinicalHealthcare.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicalHealthcare.Api.Tests.Conventions;

/// <summary>
/// AC-005 � Startup convention test.
///
/// Every route registered by an <see cref="IEndpointDefinition"/> in the API
/// assembly MUST carry either <see cref="IAuthorizeData"/> or
/// <see cref="IAllowAnonymous"/> metadata.
///
/// If this test fails, a new endpoint was wired up without an explicit
/// authorization decision � treat this as a CI pipeline failure.
/// </summary>
public sealed class EndpointAuthorizationConventionTests
{
    [Fact]
    public void AllEndpoints_HaveAuthorizationOrAllowAnonymousMetadata()
    {
        // Build a minimal host with enough services for RequestDelegateFactory
        // to resolve all handler parameters so endpoint metadata can be built.
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(_ => { });

        // Register handler parameter types so RequestDelegateFactory classifies
        // them as "from DI" rather than throwing "UNKNOWN parameter" errors.
        builder.Services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase("ConventionTest")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
        builder.Services.AddSingleton<IEmailService, NoOpEmailService>();

        var app = builder.Build();

        // Reflect over every concrete IEndpointDefinition in the API assembly
        // and map its routes onto the minimal host.
        var apiAssembly = typeof(IEndpointDefinition).Assembly;
        var definitions = apiAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface
                        && typeof(IEndpointDefinition).IsAssignableFrom(t))
            .Select(t => (IEndpointDefinition)Activator.CreateInstance(t)!)
            .ToList();

        foreach (var def in definitions)
            def.MapEndpoints(app);

        // Accessing DataSources.Endpoints triggers convention application � this
        // is the moment where .RequireAuthorization() and .AllowAnonymous()
        // metadata is written onto each RouteEndpoint.
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .ToList();

        var violations = endpoints
            .Where(ep => !ep.Metadata.Any(m => m is IAuthorizeData || m is IAllowAnonymous))
            .Select(ep => ep.DisplayName ?? "unknown")
            .ToList();

        // Known non-IEndpointDefinition routes excluded from this scan:
        //   /health  — MapHealthChecks; intentionally public (no auth metadata required)
        //   /hangfire — UseHangfireDashboard; guarded by HangfireDashboardAuthFilter (custom)
        // These are verified by code inspection and are not tracked as violations here.
        Assert.True(
            violations.Count == 0,
            $"The following endpoints are missing [Authorize] or [AllowAnonymous]:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations.Select(v => $"  - {v}")));
    }

    /// <summary>No-op email service used only to satisfy DI during metadata inference.</summary>
    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
