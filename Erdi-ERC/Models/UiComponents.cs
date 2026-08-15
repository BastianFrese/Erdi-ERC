namespace Erdi_ERC.Models.UiComponents
{
    public class F1PageHeroVm
    {
        public string? Eyebrow { get; set; }
        public bool LiveDot { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Highlight { get; set; }
        public string? Lead { get; set; }
        public List<F1ButtonVm> Actions { get; set; } = new();
        public bool Chequer { get; set; } = true;
        public bool Telemetry { get; set; } = true;
    }

    public class F1ButtonVm
    {
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = "#";
        public string Variant { get; set; } = "primary";
        public string? IconLucide { get; set; }
        public string Target { get; set; } = "";
    }

    public class F1SectionVm
    {
        public string Title { get; set; } = string.Empty;
        public string? Chip { get; set; }
        public string? SubLink { get; set; }
        public string? SubText { get; set; }
    }

    public class F1PitCardVm
    {
        public string Title { get; set; } = string.Empty;
        public string? Lead { get; set; }
        public string? Tag { get; set; }
        public string? IconLucide { get; set; }
        public string IconVariant { get; set; } = "";
        public string? Cta { get; set; }
        public string Href { get; set; } = "#";
    }

    public class F1StatTileVm
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Sub { get; set; }
        public string? IconLucide { get; set; }
        public string? Pos { get; set; }
        public string Href { get; set; } = "";
    }

    public class F1LeagueCardVm
    {
        public string Name { get; set; } = string.Empty;
        public string? SubLabel { get; set; }
        public string? Chip { get; set; }
        public string? Description { get; set; }
        public string? NextEventLabel { get; set; }
        public string? NextEventTrack { get; set; }
        public string? NextEventDate { get; set; }
        public string? NextEventFormat { get; set; }
        public string Href { get; set; } = "#";
    }
}
