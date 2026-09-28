using Erdi_ERC.Controllers;
using Erdi_ERC.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace Erdi_ERC.Tests.Controllers;

/// <summary>
/// Einreichung von Community-Highlights. Der eingesendete Link steht auf der öffentlichen
/// Seite als <c>href</c> („Clip öffnen"), und der Eintrag ist sofort sichtbar
/// (<c>IsApproved = true</c>) — ein <c>javascript:</c>-Link wäre damit Stored XSS für jeden
/// Besucher, der den Knopf anklickt. Die Prüfung selbst liegt in
/// <see cref="Erdi_ERC.Helpers.VideoEmbedHelper.SafeLinkOrNull"/> (dort getestet); hier geht
/// es darum, dass sie im Controller auch wirklich greift.
/// </summary>
public class CommunityHighlightSubmissionTests
{
    private static CommunityController BuildController(SqliteTestContext ctx)
    {
        var httpCtx = TestAuthHelper.CreateAuthenticatedContext("u1", "Fahrer");
        var ctrl = new CommunityController(ctx.Db);
        TestAuthHelper.AttachContext(ctrl, httpCtx);
        ctrl.TempData = new TempDataDictionary(httpCtx, new AdminCommunityTestHarness.NullTempDataProvider());
        return ctrl;
    }

    [Theory]
    [InlineData("javascript:fetch('/api/admin')")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("kein link")]
    public async Task SubmitHighlight_nonHttpLink_isRejectedWithMessage(string url)
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);

        var result = await ctrl.SubmitHighlight("Toller Clip", url, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(ctx.NewContext().RaceHighlightClips);
        Assert.Equal("Bitte einen vollständigen Link mit https:// eintragen.", ctrl.TempData["HighlightMessage"]);
    }

    [Fact]
    public async Task SubmitHighlight_httpLink_isStored()
    {
        using var ctx = new SqliteTestContext();
        var ctrl = BuildController(ctx);

        await ctrl.SubmitHighlight(
            "Toller Clip", "https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", "Highlight", "Imola");

        var clip = Assert.Single(ctx.NewContext().RaceHighlightClips);
        Assert.Equal("https://clips.twitch.tv/AwkwardHelplessSalamanderSwiftRage", clip.Url);
        Assert.Equal("Toller Clip", clip.Title);
        Assert.True(clip.IsApproved);
    }
}
