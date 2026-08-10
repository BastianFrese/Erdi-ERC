using <OWNER_HANDLE>_ERC.Models;
using <OWNER_HANDLE>_ERC.Models.Troll;
using Microsoft.EntityFrameworkCore;

namespace <OWNER_HANDLE>_ERC.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<League> Leagues => Set<League>();
        public DbSet<DriverStanding> DriverStandings => Set<DriverStanding>();
        public DbSet<RaceResult> RaceResults => Set<RaceResult>();
        public DbSet<RaceFinish> RaceFinishes => Set<RaceFinish>();
        public DbSet<RaceWeekend> RaceWeekends => Set<RaceWeekend>();
        public DbSet<RaceWeekendLeg> RaceWeekendLegs => Set<RaceWeekendLeg>();
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<RealLifeEvent> RealLifeEvents => Set<RealLifeEvent>();
        public DbSet<RealLifeEventImage> RealLifeEventImages => Set<RealLifeEventImage>();
        public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
        public DbSet<RaceUndoEntry> RaceUndoEntries => Set<RaceUndoEntry>();
        public DbSet<RaceReserveAssignment> RaceReserveAssignments => Set<RaceReserveAssignment>();
        public DbSet<LeaguePenalty> LeaguePenalties => Set<LeaguePenalty>();
        public DbSet<StreamSchedule> StreamSchedules => Set<StreamSchedule>();
        public DbSet<TrackSetup> TrackSetups => Set<TrackSetup>();
        public DbSet<SetupAccessRoleMapping> SetupAccessRoleMappings => Set<SetupAccessRoleMapping>();
        public DbSet<SetupBlockedUser> SetupBlockedUsers => Set<SetupBlockedUser>();
        public DbSet<CustomAchievement> CustomAchievements => Set<CustomAchievement>();
        public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
        public DbSet<DriverGamerTag> DriverGamerTags => Set<DriverGamerTag>();
        public DbSet<AchievementDefinition> AchievementDefinitions => Set<AchievementDefinition>();
        public DbSet<CommunityNewsPost> CommunityNewsPosts => Set<CommunityNewsPost>();
        public DbSet<ProfileWallMessage> ProfileWallMessages => Set<ProfileWallMessage>();
        public DbSet<RaceAvailabilityEntry> RaceAvailabilityEntries => Set<RaceAvailabilityEntry>();
        public DbSet<SetupComment> SetupComments => Set<SetupComment>();
        public DbSet<SetupLike> SetupLikes => Set<SetupLike>();
        public DbSet<CommunityVotePoll> CommunityVotePolls => Set<CommunityVotePoll>();
        public DbSet<CommunityVoteOption> CommunityVoteOptions => Set<CommunityVoteOption>();
        public DbSet<CommunityVoteResponse> CommunityVoteResponses => Set<CommunityVoteResponse>();
        public DbSet<AdminUserPermission> AdminUserPermissions => Set<AdminUserPermission>();
        public DbSet<RaceHighlightClip> RaceHighlightClips => Set<RaceHighlightClip>();
        public DbSet<DiscordWebhook> DiscordWebhooks => Set<DiscordWebhook>();
        public DbSet<AboutMeProfile> AboutMeProfiles => Set<AboutMeProfile>();
        public DbSet<WebhookAutomationRule> WebhookAutomationRules => Set<WebhookAutomationRule>();
        public DbSet<RegelwerkDocument> RegelwerkDocuments => Set<RegelwerkDocument>();
        public DbSet<DriverRoleHistory> DriverRoleHistories => Set<DriverRoleHistory>();
        public DbSet<RaceCalendarSettings> RaceCalendarSettings => Set<RaceCalendarSettings>();
        public DbSet<TrollGagOverride> TrollGagOverrides => Set<TrollGagOverride>();
        public DbSet<TrollCustomGag> TrollCustomGags => Set<TrollCustomGag>();
        public DbSet<TrollSettingsEntity> TrollSettings => Set<TrollSettingsEntity>();

        // ── Bewerbungs-Rebuild V1 (Commit 2) ─────────────────────────────────────────
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<League>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(64);
                b.Property(x => x.Name).HasMaxLength(128).IsRequired();
                b.Property(x => x.Description).HasMaxLength(1024);
                b.Property(x => x.ArchivedName).HasMaxLength(128);
                b.Property(x => x.CurrentSeason).HasMaxLength(32);

                b.HasMany(x => x.Standings)
                    .WithOne()
                    .HasForeignKey(x => x.LeagueId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasMany(x => x.Races)
                    .WithOne()
                    .HasForeignKey(x => x.LeagueId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DriverStanding>(b =>
            {
                b.HasKey(x => x.RowId);
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Driver).HasMaxLength(128).IsRequired();
                b.Property(x => x.Team).HasMaxLength(128);
                b.Property(x => x.ReserveForDriver).HasMaxLength(128);
                b.Property(x => x.PointsAdjustment).HasDefaultValue(0);
                // Hot-Query: Layout-Service & viele Controller filtern auf (LeagueId, Driver).
                b.HasIndex(x => new { x.LeagueId, x.Driver });
                b.HasIndex(x => new { x.LeagueId, x.DriverNumber }).IsUnique();
                b.HasIndex(x => x.Driver);
            });

            modelBuilder.Entity<RaceResult>(b =>
            {
                b.HasKey(x => x.RowId);
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Track).HasMaxLength(128).IsRequired();
                b.Property(x => x.Winner).HasMaxLength(128);
                b.Property(x => x.FastestLap).HasMaxLength(128);
                b.Property(x => x.Season).HasMaxLength(32);
                // Hot-Queries: Latest-Race (Layout), LeagueResults, AllRaces – alle filtern auf LeagueId und sortieren nach Date.
                b.HasIndex(x => new { x.LeagueId, x.Date });
                b.HasIndex(x => x.Date);
                b.HasMany(x => x.Finishes)
                    .WithOne()
                    .HasForeignKey(x => x.RaceResultId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasMany(x => x.ReserveAssignments)
                    .WithOne()
                    .HasForeignKey(x => x.RaceResultId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RaceFinish>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Driver).HasMaxLength(128).IsRequired();
                b.Property(x => x.RaceTimeMs).IsRequired(false);
                // Driver-Lookup für Rename + DriverDetail-Statistik.
                b.HasIndex(x => x.Driver);
            });

            modelBuilder.Entity<RaceWeekend>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Track).HasMaxLength(128).IsRequired();
                b.Property(x => x.DistancePercent).HasDefaultValue(100);
                b.HasIndex(x => x.Order);
                b.HasMany(x => x.Legs)
                    .WithOne(l => l.Weekend!)
                    .HasForeignKey(x => x.RaceWeekendId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RaceWeekendLeg>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.HasIndex(x => new { x.RaceWeekendId, x.LeagueId }).IsUnique();
                b.HasIndex(x => new { x.LeagueId, x.Date });
                b.HasIndex(x => x.Date);
            });

            modelBuilder.Entity<AdminUser>(b =>
            {
                b.HasKey(x => x.DiscordId);
                b.Property(x => x.DiscordId).HasMaxLength(32);
                b.Property(x => x.DisplayName).HasMaxLength(128);
                b.Property(x => x.IsSuperAdmin).HasDefaultValue(false);
                b.HasMany(x => x.Permissions)
                    .WithOne(p => p.AdminUser)
                    .HasForeignKey(p => p.DiscordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AdminUserPermission>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.Permission).HasMaxLength(64).IsRequired();
                b.HasIndex(x => new { x.DiscordId, x.Permission }).IsUnique();
            });

            modelBuilder.Entity<RealLifeEvent>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(256).IsRequired();
                b.Property(x => x.Location).HasMaxLength(256);
                b.Property(x => x.Description).HasColumnType("TEXT");
                b.Property(x => x.ImageFileName).HasMaxLength(256);
                b.Property(x => x.YouTubeUrl).HasMaxLength(512);
                b.HasMany(x => x.Images)
                    .WithOne()
                    .HasForeignKey(x => x.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RealLifeEventImage>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.FileName).HasMaxLength(256).IsRequired();
                b.Property(x => x.Caption).HasMaxLength(512);
            });

            modelBuilder.Entity<AdminAuditLog>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Actor).HasMaxLength(128).IsRequired();
                b.Property(x => x.Action).HasMaxLength(128).IsRequired();
                b.Property(x => x.EntityType).HasMaxLength(128);
                b.Property(x => x.EntityId).HasMaxLength(128);
                b.Property(x => x.Details).HasMaxLength(2048);
                b.HasIndex(x => x.CreatedAt);
                b.HasIndex(x => new { x.EntityType, x.EntityId });
            });

            modelBuilder.Entity<RaceUndoEntry>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Actor).HasMaxLength(128).IsRequired();
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.PayloadJson).HasColumnType("LONGTEXT").IsRequired();
                b.HasIndex(x => new { x.LeagueId, x.IsUsed, x.CreatedAt });
            });

            modelBuilder.Entity<RaceReserveAssignment>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.ReserveDriver).HasMaxLength(128).IsRequired();
                b.Property(x => x.MainDriver).HasMaxLength(128).IsRequired();
                b.HasIndex(x => new { x.RaceResultId, x.ReserveDriver }).IsUnique();
                b.HasIndex(x => new { x.RaceResultId, x.MainDriver });
            });

            modelBuilder.Entity<LeaguePenalty>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Driver).HasMaxLength(128).IsRequired();
                b.Property(x => x.DriverNumber);
                b.Property(x => x.PenaltyType).HasMaxLength(64).IsRequired();
                b.Property(x => x.RaceTrack).HasMaxLength(128);
                b.Property(x => x.SecondDriver).HasMaxLength(128);
                b.Property(x => x.SecondDriverNumber);
                b.Property(x => x.Incident).HasMaxLength(2048);
                b.Property(x => x.Reason).HasMaxLength(1024).IsRequired();
                b.Property(x => x.CreatedBy).HasMaxLength(128);
                b.HasIndex(x => new { x.LeagueId, x.Date });
                b.HasIndex(x => new { x.IsPublic, x.Date });
            });

            modelBuilder.Entity<StreamSchedule>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(256).IsRequired();
                b.Property(x => x.Url).HasMaxLength(512);
                b.Property(x => x.TimeOfDay).IsRequired(false);
                b.HasIndex(x => x.StartAt);
                b.HasIndex(x => new { x.IsRecurring, x.DayOfWeek });
            });

            modelBuilder.Entity<TrackSetup>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Track).HasMaxLength(128).IsRequired();
                b.Property(x => x.Title).HasMaxLength(256).IsRequired();
                b.Property(x => x.GameYear).HasMaxLength(16).IsRequired(false);
                b.Property(x => x.SetupInfo).HasMaxLength(1024);
                b.Property(x => x.SetupText).HasColumnType("LONGTEXT").IsRequired();
                b.Property(x => x.RequiredRoleLabel).HasMaxLength(128);
                b.HasIndex(x => new { x.Track, x.GameYear, x.RequiredAccessTier });
                b.HasIndex(x => x.UpdatedAt);
            });

            modelBuilder.Entity<SetupAccessRoleMapping>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Tier).IsRequired();
                b.Property(x => x.RoleId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Label).HasMaxLength(128);
                b.HasIndex(x => x.RoleId).IsUnique();
                b.HasIndex(x => x.Tier);
            });

            modelBuilder.Entity<SetupBlockedUser>(b =>
            {
                b.HasKey(x => x.DiscordId);
                b.Property(x => x.DiscordId).HasMaxLength(32);
                b.Property(x => x.Reason).HasMaxLength(256);
            });

            modelBuilder.Entity<DriverProfile>(b =>
            {
                b.HasKey(x => x.DiscordId);
                b.Property(x => x.DiscordId).HasMaxLength(32);
                b.Property(x => x.DiscordName).HasMaxLength(128).IsRequired();
                b.Property(x => x.DisplayName).HasMaxLength(128);
                b.Property(x => x.FavoriteTrack).HasMaxLength(128);
                b.Property(x => x.InputDevice).HasMaxLength(64);
                b.Property(x => x.PreferredPlatform).HasMaxLength(64);
                b.Property(x => x.Nationality).HasMaxLength(64);
                b.Property(x => x.Bio).HasMaxLength(512);
                b.Property(x => x.DriverNumberColor).HasMaxLength(7);
                // FindByDriverNameAsync lookup.
                b.HasIndex(x => x.DisplayName);
                b.HasMany(x => x.GamerTags)
                    .WithOne()
                    .HasForeignKey(x => x.DiscordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DriverGamerTag>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.Platform).HasMaxLength(64).IsRequired();
                b.Property(x => x.GamerTag).HasMaxLength(128).IsRequired();
                b.Property(x => x.LinkedByDiscordId).HasMaxLength(32);
                b.HasIndex(x => new { x.DiscordId, x.Platform }).IsUnique();
                b.HasIndex(x => x.GamerTag);
            });

            modelBuilder.Entity<AchievementDefinition>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Key).HasMaxLength(64).IsRequired();
                b.Property(x => x.Title).HasMaxLength(128).IsRequired();
                b.Property(x => x.Description).HasMaxLength(512).IsRequired();
                b.Property(x => x.Icon).HasMaxLength(64);
                b.Property(x => x.Tone).HasMaxLength(32);
                b.Property(x => x.Tier).HasMaxLength(32);
                b.Property(x => x.Category).HasMaxLength(64);
                b.HasIndex(x => x.Key).IsUnique();
            });

            modelBuilder.Entity<CommunityNewsPost>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(160).IsRequired();
                b.Property(x => x.Category).HasMaxLength(96);
                b.Property(x => x.Summary).HasMaxLength(480);
                b.Property(x => x.Content).HasColumnType("LONGTEXT").IsRequired();
                b.Property(x => x.AuthorName).HasMaxLength(128);
                b.HasIndex(x => new { x.IsPublished, x.IsPinned, x.PublishedAt });
            });

            modelBuilder.Entity<ProfileWallMessage>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.ProfileDiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.AuthorDiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.AuthorName).HasMaxLength(128).IsRequired();
                b.Property(x => x.Message).HasMaxLength(600).IsRequired();
                b.HasIndex(x => new { x.ProfileDiscordId, x.CreatedAt });
            });

            modelBuilder.Entity<RaceAvailabilityEntry>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.DiscordName).HasMaxLength(128).IsRequired();
                b.Property(x => x.Status).HasMaxLength(24).IsRequired();
                b.Property(x => x.Note).HasMaxLength(240);
                b.HasIndex(x => new { x.EventId, x.DiscordId }).IsUnique();
                b.HasIndex(x => new { x.EventId, x.Status });
            });

            modelBuilder.Entity<SetupComment>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.AuthorDiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.AuthorName).HasMaxLength(128).IsRequired();
                b.Property(x => x.Message).HasMaxLength(600).IsRequired();
                b.HasIndex(x => new { x.TrackSetupId, x.CreatedAt });
            });

            modelBuilder.Entity<SetupLike>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.DiscordName).HasMaxLength(128);
                b.HasIndex(x => new { x.TrackSetupId, x.DiscordId }).IsUnique();
            });

            modelBuilder.Entity<CommunityVotePoll>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(160).IsRequired();
                b.Property(x => x.Category).HasMaxLength(96);
                b.Property(x => x.Description).HasMaxLength(480);
                b.HasMany(x => x.Options)
                    .WithOne()
                    .HasForeignKey(x => x.PollId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CommunityVoteOption>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Label).HasMaxLength(160).IsRequired();
                b.HasIndex(x => x.PollId);
            });

            modelBuilder.Entity<CommunityVoteResponse>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.DiscordName).HasMaxLength(128).IsRequired();
                b.HasIndex(x => new { x.PollId, x.DiscordId }).IsUnique();
            });

            modelBuilder.Entity<RaceHighlightClip>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(160).IsRequired();
                b.Property(x => x.Url).HasMaxLength(512).IsRequired();
                b.Property(x => x.Category).HasMaxLength(96);
                b.Property(x => x.RaceLabel).HasMaxLength(160);
                b.Property(x => x.SubmittedByDiscordId).HasMaxLength(32);
                b.Property(x => x.SubmittedByName).HasMaxLength(128).IsRequired();
                b.HasIndex(x => new { x.IsApproved, x.CreatedAt });
            });

            modelBuilder.Entity<RegelwerkDocument>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Title).HasMaxLength(200).IsRequired();
                b.Property(x => x.Version).HasMaxLength(20);
                b.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
                b.Property(x => x.OriginalFileName).HasMaxLength(260);
                b.Property(x => x.ContentType).HasMaxLength(100);
                b.Property(x => x.UploadedBy).HasMaxLength(128);
                b.HasIndex(x => x.IsActive);
            });

            modelBuilder.Entity<DriverRoleHistory>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Driver).HasMaxLength(128).IsRequired();
                b.Property(x => x.PreviousRole).HasMaxLength(64);
                b.Property(x => x.NewRole).HasMaxLength(64).IsRequired();
                b.Property(x => x.Reason).HasMaxLength(500);
                b.Property(x => x.ChangedBy).HasMaxLength(128);
                b.HasIndex(x => new { x.LeagueId, x.Driver });
            });

            // ── <OWNER_HANDLE>-Troll-System (admin-verwaltet) ───────────────────────────────────
            modelBuilder.Entity<TrollGagOverride>(b =>
            {
                b.HasKey(x => x.Key);
                b.Property(x => x.Key).HasMaxLength(64);
            });

            modelBuilder.Entity<TrollCustomGag>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Key).HasMaxLength(64).IsRequired();
                b.Property(x => x.Title).HasMaxLength(200).IsRequired();
                b.Property(x => x.Eyebrow).HasMaxLength(80);
                b.Property(x => x.Lead).HasMaxLength(400);
                b.Property(x => x.Body).HasColumnType("TEXT");
                b.Property(x => x.Question).HasMaxLength(400);
                b.Property(x => x.Answer).HasMaxLength(200);
                b.Property(x => x.OptionsJson).HasColumnType("TEXT");
                b.Property(x => x.CreatedBy).HasMaxLength(128);
                b.HasIndex(x => x.Key).IsUnique();
            });

            modelBuilder.Entity<TrollSettingsEntity>(b =>
            {
                b.HasKey(x => x.Id);
            });

            // ── Bewerbungs-Rebuild V1 (Commit 2) ─────────────────────────────────────────
            modelBuilder.Entity<Application>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(64);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.DiscordName).HasMaxLength(128).IsRequired();
                b.Property(x => x.GamerTag).HasMaxLength(128).IsRequired();
                b.Property(x => x.Platform).HasMaxLength(64).IsRequired();
                b.Property(x => x.TargetLeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Role).HasMaxLength(32).IsRequired();
                b.Property(x => x.Motivation).HasMaxLength(2000);
                b.Property(x => x.DecidedByDiscordId).HasMaxLength(32);
                b.Property(x => x.ReviewNote).HasMaxLength(1000);
                b.Property(x => x.DiscordJoinWarningDetail).HasMaxLength(500);

                // Dedup-Lookup bei Re-Apply, Admin-Listen-Queries.
                b.HasIndex(x => new { x.DiscordId, x.Status });
                b.HasIndex(x => new { x.Status, x.CreatedAt });
                b.HasIndex(x => new { x.TargetLeagueId, x.Status });

                b.HasOne(x => x.TargetLeague)
                    .WithMany()
                    .HasForeignKey(x => x.TargetLeagueId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<WaitlistEntry>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasMaxLength(64);
                b.Property(x => x.DiscordId).HasMaxLength(32).IsRequired();
                b.Property(x => x.DiscordName).HasMaxLength(128).IsRequired();
                b.Property(x => x.GamerTag).HasMaxLength(128).IsRequired();
                b.Property(x => x.Platform).HasMaxLength(64).IsRequired();
                b.Property(x => x.LeagueId).HasMaxLength(64).IsRequired();
                b.Property(x => x.Note).HasMaxLength(500);
                b.Property(x => x.PromotedToApplicationId).HasMaxLength(64);

                // Geordnete Warteliste pro Liga + Dedup pro User/Liga.
                b.HasIndex(x => new { x.LeagueId, x.Position });
                b.HasIndex(x => new { x.DiscordId, x.LeagueId });

                b.HasOne(x => x.League)
                    .WithMany()
                    .HasForeignKey(x => x.LeagueId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
