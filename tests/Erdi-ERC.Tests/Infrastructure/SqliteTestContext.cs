using Erdi_ERC.Data;
using Erdi_ERC.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Erdi_ERC.Tests.Infrastructure;

/// <summary>
/// Stellt einen <see cref="AppDbContext"/> auf einer frischen SQLite-In-Memory-DB bereit.
/// Im Gegensatz zum EF-InMemory-Provider unterstützt SQLite echte Transaktionen, die
/// <c>ApplicationWorkflowService</c> (Accept/Reopen/MoveToLeague) verwendet.
///
/// Die Connection bleibt für die Lebensdauer offen — sonst verwirft SQLite die
/// In-Memory-Datenbank, sobald die letzte Verbindung schließt.
/// </summary>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>Primärer Context, den der getestete Service nutzt.</summary>
    public AppDbContext Db { get; }

    public SqliteTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = Create(_connection);
        Db.Database.EnsureCreated();
    }

    /// <summary>
    /// Frischer Context auf derselben DB — zum Verifizieren echter Persistenz
    /// (statt nur des ChangeTracker-Zustands des Schreib-Contexts).
    /// </summary>
    public AppDbContext NewContext() => Create(_connection);

    private static AppDbContext Create(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            // Muss dem Prod-Default aus Program.cs entsprechen — sonst übersehen
            // Tests Silent-Write-Bugs (untracked Entity mutiert, SaveChanges schreibt nichts).
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
            // SQLite generiert keine [Timestamp]/RowVersion-Werte — der Interceptor
            // füllt sie, damit Concurrency-Token-Updates nicht ins Leere greifen.
            .AddInterceptors(new RowVersionInterceptor())
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>
/// Setzt RowVersion-Token für geänderte <see cref="ApplicationForm"/>-Entitäten vor dem
/// Speichern. MySQL erledigt das in Produktion serverseitig; SQLite kennt keine
/// rowversion-Spalten, daher dieser Test-Ersatz. Der CurrentValue wird gesetzt, der
/// OriginalValue bleibt der geladene Wert → das WHERE … RowVersion = @original matcht weiter.
/// </summary>
internal sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Apply(DbContext? context)
    {
        if (context is null) return;
    }
}
