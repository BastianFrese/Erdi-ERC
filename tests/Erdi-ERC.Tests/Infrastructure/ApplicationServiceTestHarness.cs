using <OWNER_HANDLE>_ERC.Data;
using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace <OWNER_HANDLE>_ERC.Tests.Infrastructure;

/// <summary>No-op-Webhook-Service: keine echten HTTP-Calls in Tests.</summary>
internal sealed class FakeWebhookAutomationService : IWebhookAutomationService
{
    public Task FireAsync(string eventType, Dictionary<string, string> vars) => Task.CompletedTask;

    public Task<(bool Success, string? Error)> TestRuleAsync(
        int ruleId, Dictionary<string, string> vars, CancellationToken ct = default)
        => Task.FromResult<(bool, string?)>((true, null));
}

/// <summary>
/// Baut einen <see cref="ApplicationManagementService"/> mit echten Kollaborateuren
/// (DriverProfileService, AdminAuditService) auf demselben Context — nur Webhooks sind gefakt.
/// So testen wir die tatsächlichen DB-Pfade inkl. Audit-Persistenz und Standing-Eintrag.
/// </summary>
internal static class ApplicationServiceTestHarness
{
    public static ApplicationManagementService CreateService(AppDbContext db)
    {
        var driverProfiles = new DriverProfileService(
            db, Microsoft.Extensions.Options.Options.Create(new DriverMatchingOptions()));
        var audit = new AdminAuditService(db, new HttpContextAccessor());
        var webhook = new FakeWebhookAutomationService();
        return new ApplicationManagementService(db, driverProfiles, audit, webhook);
    }

    public static League SeedLeague(AppDbContext db, string id, string name, bool isArchived = false)
    {
        var league = new League { Id = id, Name = name, IsArchived = isArchived };
        db.Leagues.Add(league);
        db.SaveChanges();
        return league;
    }

    public static ApplicationForm SeedApplication(
        AppDbContext db,
        string gamingName = "TestDriver",
        string? appliedLeagueId = null,
        string division = "Rookie Crossplay Division 3",
        string role = "Stammfahrer",
        string? discordId = "discord-1",
        bool isAccepted = false,
        bool isRejected = false,
        bool isFlagged = false)
    {
        var app = new ApplicationForm
        {
            Age = 25,
            DiscordName = $"{gamingName}***REMOVED***0001",
            DiscordId = discordId,
            Role = role,
            GamingName = gamingName,
            Platform = "EA",
            AiLevel = "100",
            Division = division,
            AppliedLeagueId = appliedLeagueId,
            Status = isRejected ? ApplicationStatus.Rejected
                   : isAccepted ? ApplicationStatus.Accepted
                   : ApplicationStatus.Open,
            AcceptedAt = isAccepted ? DateTime.UtcNow : null,
            RejectedAt = isRejected ? DateTime.UtcNow : null,
            IsFlagged = isFlagged,
            FlaggedAt = isFlagged ? DateTime.UtcNow : null,
            SubmittedAt = DateTime.UtcNow
        };
        db.ApplicationForms.Add(app);
        db.SaveChanges();
        return app;
    }
}
