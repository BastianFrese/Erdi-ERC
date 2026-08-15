namespace Erdi_ERC.Options
{
    /// <summary>
    /// Rate-Limiting-Schwellwerte (Fixed-Window pro IP).
    /// Sektion: <c>RateLimiting</c>
    /// </summary>
    public sealed class RateLimitingOptions
    {
        public const string SectionName = "RateLimiting";

        public RateLimiterBucket Global { get; set; } = new() { PermitLimit = 200, WindowSeconds = 60 };
        public RateLimiterBucket Auth   { get; set; } = new() { PermitLimit = 10,  WindowSeconds = 60 };
        public RateLimiterBucket Forms  { get; set; } = new() { PermitLimit = 20,  WindowSeconds = 60 };

        public sealed class RateLimiterBucket
        {
            public int PermitLimit { get; set; }
            public int WindowSeconds { get; set; }
        }
    }
}
