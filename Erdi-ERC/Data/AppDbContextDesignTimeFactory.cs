using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace <OWNER_HANDLE>_ERC.Data
{
    /// <summary>
    /// Wird vom EF-Tooling (<c>dotnet ef migrations …</c> und <c>dotnet ef database update</c>) verwendet.
    /// Liest den Connection-String aus der Konfiguration (appsettings + Environment-Variablen).
    /// Nur wenn kein String konfiguriert ist, wird ein Offline-Dummy genutzt, damit Migrationen
    /// ohne laufende DB generiert werden können.
    /// Zur Laufzeit kommt diese Factory nicht zum Einsatz (siehe Program.cs / AddDbContext).
    /// </summary>
    public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config.GetConnectionString("Default");

            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = "Server=localhost;Port=3306;Database=erdierc;User=root;Password=design-time;";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
                .Options;

            return new AppDbContext(options);
        }
    }
}
