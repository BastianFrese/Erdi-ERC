using Erdi_ERC.Models.UiComponents;

namespace Erdi_ERC.Tests.Frontend;

/// <summary>
/// Der Turm steht auf jeder oeffentlichen Seite und zeigt die ersten N Fahrer
/// der Meisterschaft. Er darf nie werfen und nie mehr Zeilen zeigen als bestellt —
/// ein Turm, der auf einer Seite die Hoehe sprengt, faellt ueberall auf.
/// </summary>
public class TowerVmTests
{
    private static IReadOnlyList<TowerRowVm> Zeilen(int n) =>
        Enumerable.Range(1, n)
            .Select(i => new TowerRowVm(i, $"Fahrer{i}", "ferrari", "Ferrari", 100 - i))
            .ToArray();

    [Fact]
    public void Turm_Kuerzt_Auf_Die_Bestellten_Zeilen()
    {
        var vm = new TowerVm("Meisterschaft", Zeilen(20), maxRows: 10);

        Assert.Equal(10, vm.Rows.Count);
        Assert.Equal(1, vm.Rows[0].Pos);
        Assert.Equal(10, vm.Rows[^1].Pos);
    }

    [Fact]
    public void Turm_Sortiert_Nach_Position()
    {
        // Die Wertung kommt aus der Datenbank; die Reihenfolge darf nicht davon abhaengen.
        var durcheinander = Zeilen(5).Reverse().ToArray();
        var vm = new TowerVm("Meisterschaft", durcheinander, maxRows: 5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, vm.Rows.Select(r => r.Pos));
    }

    [Fact]
    public void Turm_Vertraegt_Eine_Leere_Wertung()
    {
        // Saison noch nicht gestartet: kein Absturz, keine Zeilen.
        var vm = new TowerVm("Meisterschaft", Array.Empty<TowerRowVm>(), maxRows: 10);

        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void Turm_Vertraegt_Null_Statt_Einer_Wertung()
    {
        var vm = new TowerVm("Meisterschaft", null, maxRows: 10);

        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void Turm_Kuerzt_Auf_Zwanzig_Wenn_maxRows_Null_Ist()
    {
        // Untergrenze fuer den Schutz gegen versehentlich riesige Werte.
        var vm = new TowerVm("Meisterschaft", Zeilen(50), maxRows: 0);
        Assert.True(vm.Rows.Count <= 20, $"unerwartet viele Zeilen: {vm.Rows.Count}");
    }
}
