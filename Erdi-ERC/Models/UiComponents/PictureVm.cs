namespace Erdi_ERC.Models.UiComponents;

/// <summary>
/// ViewModel für das _Picture-Partial. Kapselt alle Attribute, die ein &lt;picture&gt;-Element braucht.
/// </summary>
public class PictureVm
{
    /// <summary>Pflicht: Original-Bild-URL (relativ zur Site-Root, mit führendem /).</summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>Alt-Text für das &lt;img&gt;.</summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>CSS-Klasse für das innere &lt;img&gt;.</summary>
    public string? CssClass { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>sizes-Attribut. Default: 100vw.</summary>
    public string Sizes { get; set; } = "100vw";

    /// <summary>Loading-Attribut. Default: lazy.</summary>
    public string Loading { get; set; } = "lazy";

    /// <summary>Decoding-Attribut. Default: async.</summary>
    public string Decoding { get; set; } = "async";

    /// <summary>"high" → loading=eager + fetchpriority=high (für LCP).</summary>
    public string? Priority { get; set; }

    /// <summary>Target-Breiten (px). Default: 640,1280,2560.</summary>
    public string Widths { get; set; } = "640,1280,2560";

    /// <summary>Zusätzliche Attribute, die in das &lt;img&gt; durchgereicht werden (data-*).</summary>
    public string? ExtraAttrs { get; set; }
}
