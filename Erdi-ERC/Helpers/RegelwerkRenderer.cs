using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Erdi_ERC.Helpers
{
    /// <summary>
    /// Zerlegt ein Markdown-Dokument anhand seiner H1/H2-Überschriften in Sektionen,
    /// die dann als individuelle doc-cards im Site-Stil gerendert werden können.
    /// </summary>
    public static class RegelwerkRenderer
    {
        private static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public record Section(string HeadingHtml, string BodyHtml, int Level, int Index);

        /// <summary>
        /// Parst <paramref name="markdownText"/> und gibt eine Liste von Abschnitten zurück.
        /// Jede Top-Level-Überschrift (H1 oder H2) beginnt einen neuen Abschnitt.
        /// Inhalte vor der ersten Überschrift landen in einem Intro-Abschnitt ohne Heading.
        /// </summary>
        public static List<Section> Parse(string markdownText)
        {
            var document = Markdig.Markdown.Parse(markdownText, Pipeline);
            var sections = new List<Section>();

            var currentHeadingHtml = string.Empty;
            var currentHeadingLevel = 0;
            var currentBlocks = new List<Block>();
            var sectionIndex = 0;

            void Flush()
            {
                if (currentBlocks.Count == 0 && string.IsNullOrEmpty(currentHeadingHtml))
                    return;

                var bodyHtml = RenderBlocks(currentBlocks);
                sections.Add(new Section(currentHeadingHtml, bodyHtml, currentHeadingLevel, sectionIndex++));
                currentBlocks.Clear();
            }

            foreach (var block in document)
            {
                if (block is HeadingBlock heading && heading.Level <= 2)
                {
                    Flush();
                    currentHeadingHtml = RenderInlines(heading);
                    currentHeadingLevel = heading.Level;
                }
                else
                {
                    currentBlocks.Add(block);
                }
            }

            Flush();
            return sections;
        }

        private static string RenderInlines(HeadingBlock heading)
        {
            if (heading.Inline == null) return string.Empty;
            using var sw = new StringWriter();
            var renderer = new Markdig.Renderers.HtmlRenderer(sw);
            renderer.ObjectRenderers.TryFind<Markdig.Renderers.Html.Inlines.LiteralInlineRenderer>(out _);
            renderer.Render(heading.Inline);
            return sw.ToString().Trim();
        }

        private static string RenderBlocks(List<Block> blocks)
        {
            if (blocks.Count == 0) return string.Empty;
            using var sw = new StringWriter();
            var renderer = new Markdig.Renderers.HtmlRenderer(sw);
            // Register default renderers
            new Markdig.Renderers.HtmlRenderer(new StringWriter()); // warm-up not needed; use setup below
            var setup = new Markdig.Extensions.Tables.HtmlTableRenderer();
            _ = setup; // unused, renderers are auto-registered via pipeline
            foreach (var block in blocks)
                renderer.Render(block);
            return sw.ToString();
        }
    }
}
