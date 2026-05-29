using <OWNER_HANDLE>_ERC.Helpers;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Smoke-Tests, die das Test-Setup (xunit + ProjectReference) verifizieren,
/// ohne die ASP.NET-App zu booten (Program.cs startet Migrations -> braucht echte DB).
///
/// Fuer echte Integrationstests mit WebApplicationFactor&lt;Program&gt; muesste
/// Program.cs erst so refactored werden, dass DB-Migrate optional / mockbar ist.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void TestProject_Bootstraps()
    {
        // Wenn dieser Test laeuft, kompiliert das Test-Projekt, die ProjectReference
        // auf <OWNER_HANDLE>-ERC funktioniert und xUnit ist korrekt eingebunden.
        Assert.True(true);
    }

    [Fact]
    public void F1TeamsHelper_TypeIsAccessibleFromTests()
    {
        // Verifiziert nur, dass Symbole aus <OWNER_HANDLE>-ERC im Test-Projekt sichtbar sind.
        var type = typeof(F1TeamsHelper);
        Assert.NotNull(type);
        Assert.Equal("<OWNER_HANDLE>_ERC.Helpers", type.Namespace);
    }
}
