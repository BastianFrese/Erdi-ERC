using <OWNER_HANDLE>_ERC.Models.Troll;
using <OWNER_HANDLE>_ERC.Options;
using <OWNER_HANDLE>_ERC.Services;

namespace <OWNER_HANDLE>_ERC.Tests;

/// <summary>
/// Sichert die reine Logik des <see cref="TrollService"/> ab: Auslöse-Würfel,
/// gewichtete Gag-Auswahl, einstellige Mathe-Aufgaben und Antwort-Validierung.
/// Die RNG-freien Helfer (ShouldTriggerForRoll/PickGagForRoll) sind deterministisch.
/// </summary>
public class TrollServiceTests
{
    private static TrollService Make(TrollOptions? options = null)
        => new(Microsoft.Extensions.Options.Options.Create(options ?? new TrollOptions()));

    // Alle Gag-Keys auf 0 → Test der „alles deaktiviert"-Fälle.
    // WICHTIG: muss exakt den Katalog in TrollService spiegeln, sonst greift ein nicht
    // genullter Gag und der „alles aus"-Fallback-Test schlägt fehl.
    private static Dictionary<string, int> AllDisabled() => new()
    {
        ["math"] = 0, ["best-driver"] = 0, ["f1-trivia"] = 0, ["sequence"] = 0, ["oath-type"] = 0,
        ["fake-loading"] = 0, ["fake-demotion"] = 0, ["fake-terms"] = 0,
        ["bluescreen"] = 0, ["fake-update"] = 0, ["fake-ban"] = 0,
        ["reaction"] = 0, ["not-a-bot"] = 0, ["runaway-button"] = 0,
        ["hold-button"] = 0, ["whack-erdi"] = 0, ["red-button"] = 0,
        ["wheel"] = 0, ["fortune"] = 0, ["dice"] = 0,
        ["magic-8ball"] = 0, ["horoscope"] = 0, ["coinflip"] = 0
    };

    // ---------- Auslöse-Würfel ----------

    [Fact]
    public void ShouldTriggerForRoll_BelowChance_ReturnsTrue()
    {
        var svc = Make(new TrollOptions { TriggerChance = 0.25 });
        Assert.True(svc.ShouldTriggerForRoll(0.1));
    }

    [Fact]
    public void ShouldTriggerForRoll_AtOrAboveChance_ReturnsFalse()
    {
        var svc = Make(new TrollOptions { TriggerChance = 0.25 });
        Assert.False(svc.ShouldTriggerForRoll(0.25)); // strikt < chance
        Assert.False(svc.ShouldTriggerForRoll(0.9));
    }

    [Fact]
    public void ShouldTriggerForRoll_Disabled_AlwaysFalse()
    {
        var svc = Make(new TrollOptions { Enabled = false, TriggerChance = 1.0 });
        Assert.False(svc.ShouldTriggerForRoll(0.0));
    }

    // ---------- Gewichtete Auswahl ----------

    [Fact]
    public void PickGagForRoll_RespectsWeightBoundaries()
    {
        // Nur math(5) und dice(5) aktiv → Reihenfolge folgt dem Katalog (math vor dice).
        var weights = AllDisabled();
        weights["math"] = 5;
        weights["dice"] = 5;
        var svc = Make(new TrollOptions { Weights = weights });

        Assert.Equal("math", svc.PickGagForRoll(0).Key);
        Assert.Equal("math", svc.PickGagForRoll(4).Key);
        Assert.Equal("dice", svc.PickGagForRoll(5).Key);
        Assert.Equal("dice", svc.PickGagForRoll(9).Key);
    }

    [Fact]
    public void PickGagForRoll_ZeroWeightGag_NeverSelected()
    {
        var weights = AllDisabled();
        weights["math"] = 5;
        weights["dice"] = 5;
        var svc = Make(new TrollOptions { Weights = weights });

        for (var roll = 0; roll < 10; roll++)
        {
            var key = svc.PickGagForRoll(roll).Key;
            Assert.True(key is "math" or "dice", $"Unerwarteter Gag {key} bei roll {roll}");
        }
    }

    [Fact]
    public void PickGag_AllDisabled_FallsBackToMath()
    {
        var svc = Make(new TrollOptions { Weights = AllDisabled() });
        Assert.Equal("math", svc.PickGag().Key);
    }

    // ---------- Mathe-Aufgabe ----------

    [Fact]
    public void BuildChallenge_Math_IsSingleDigitAdditionWithCorrectSum()
    {
        var svc = Make(new TrollOptions { MathMaxOperand = 9 });
        var gag = svc.FindGag("math")!;

        for (var i = 0; i < 200; i++)
        {
            var challenge = svc.BuildChallenge(gag);

            Assert.Equal("math", challenge.GagKey);
            Assert.NotNull(challenge.Prompt);

            var parts = challenge.Prompt!.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, parts.Length);

            var a = int.Parse(parts[0]);
            var b = int.Parse(parts[1]);
            Assert.InRange(a, 1, 9);
            Assert.InRange(b, 1, 9);
            Assert.Equal((a + b).ToString(), challenge.ExpectedAnswer);
        }
    }

    [Fact]
    public void BuildChallenge_BestDriver_Expects<OWNER_HANDLE>()
        => Assert.Equal("<OWNER_HANDLE>", Make().BuildChallenge(Make().FindGag("best-driver")!).ExpectedAnswer);

    [Fact]
    public void BuildChallenge_Sequence_IsArithmeticWithCorrectNext()
    {
        var svc = Make();
        var gag = svc.FindGag("sequence")!;

        for (var i = 0; i < 200; i++)
        {
            var challenge = svc.BuildChallenge(gag);

            Assert.Equal("sequence", challenge.GagKey);
            Assert.NotNull(challenge.Prompt);

            var nums = challenge.Prompt!
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();

            Assert.Equal(4, nums.Length);
            var step = nums[1] - nums[0];
            Assert.True(step > 0, "Schrittweite muss positiv sein");
            for (var k = 1; k < nums.Length; k++)
            {
                Assert.Equal(step, nums[k] - nums[k - 1]); // konstante Differenz
            }
            Assert.Equal((nums[^1] + step).ToString(), challenge.ExpectedAnswer);
        }
    }

    [Fact]
    public void BuildChallenge_OathType_PromptEqualsExpectedAnswer()
    {
        var challenge = Make().BuildChallenge(Make().FindGag("oath-type")!);
        Assert.False(string.IsNullOrWhiteSpace(challenge.ExpectedAnswer));
        Assert.Equal(challenge.Prompt, challenge.ExpectedAnswer); // muss exakt abgetippt werden
    }

    [Fact]
    public void BuildChallenge_F1Trivia_AnswerMatchesAStoredQuestion()
    {
        var svc = Make();
        var gag = svc.FindGag("f1-trivia")!;

        for (var i = 0; i < 100; i++)
        {
            var challenge = svc.BuildChallenge(gag);

            Assert.NotNull(challenge.Prompt);
            var question = <OWNER_HANDLE>_ERC.Models.Troll.TrollTrivia.ByQuestion(challenge.Prompt);
            Assert.NotNull(question); // Prompt muss eine echte Frage aus der Bank sein
            Assert.Equal(question!.Answer, challenge.ExpectedAnswer);
            Assert.Contains(challenge.ExpectedAnswer, question.Options); // korrekte Antwort ist eine der Optionen
        }
    }

    [Fact]
    public void TrollTrivia_EveryQuestion_HasAnswerAmongOptions()
    {
        Assert.NotEmpty(<OWNER_HANDLE>_ERC.Models.Troll.TrollTrivia.Questions);
        foreach (var q in <OWNER_HANDLE>_ERC.Models.Troll.TrollTrivia.Questions)
        {
            Assert.Contains(q.Answer, q.Options);
        }
    }

    [Fact]
    public void BuildChallenge_NonBlockingGag_HasNoExpectedAnswer()
    {
        var svc = Make();
        Assert.Null(svc.BuildChallenge(svc.FindGag("dice")!).ExpectedAnswer);
    }

    // ---------- Antwort-Validierung ----------

    [Theory]
    [InlineData("12", "12", true)]
    [InlineData("12", " 12 ", true)]   // getrimmt
    [InlineData("<OWNER_HANDLE>", "erdi", true)] // Groß-/Kleinschreibung egal
    [InlineData("12", "13", false)]
    [InlineData("12", null, false)]
    [InlineData(null, "12", false)]    // keine erwartete Antwort → nie korrekt
    public void IsAnswerCorrect_Cases(string? expected, string? userAnswer, bool expectedResult)
        => Assert.Equal(expectedResult, Make().IsAnswerCorrect(expected, userAnswer));

    // ---------- Konfig-Spiegelung ----------

    [Fact]
    public void FindGag_KnownAndUnknownKeys()
    {
        var svc = Make();
        Assert.Equal("math", svc.FindGag("math")!.Key);
        Assert.Null(svc.FindGag("does-not-exist"));
        Assert.Null(svc.FindGag(null));
    }

    [Fact]
    public void MercyAfterAttempts_IsClampedToAtLeastOne()
        => Assert.True(Make(new TrollOptions { MercyAfterAttempts = 0 }).MercyAfterAttempts >= 1);

    [Fact]
    public void AppliesToAdmins_ReflectsOption()
    {
        Assert.False(Make(new TrollOptions { ApplyToAdmins = false }).AppliesToAdmins);
        Assert.True(Make(new TrollOptions { ApplyToAdmins = true }).AppliesToAdmins);
    }
}
