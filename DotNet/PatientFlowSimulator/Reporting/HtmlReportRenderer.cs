using System.Net;
using Markdig;

namespace LantanaGroup.Link.PatientFlowSimulator.Reporting;

/// <summary>
/// Wraps the Markdown report in a standalone HTML page that renders its Mermaid charts itself, for
/// viewers whose Markdown preview cannot. The text is static HTML; only the charts need the network,
/// to load Mermaid from a CDN.
/// </summary>
public static class HtmlReportRenderer
{
    /// <summary>
    /// Mermaid build the page loads. Pinned to a version the report's charts have been checked against.
    /// </summary>
    public const string MermaidScriptUrl = "https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.min.js";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// The page for a Markdown report.
    /// </summary>
    public static string Render(string markdown, string title)
    {
        var body = Markdown.ToHtml(markdown, Pipeline);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{WebUtility.HtmlEncode(title)}}</title>
            <style>
            {{Css}}
            </style>
            </head>
            <body>
            <nav id="facilities"></nav>
            <main>
            {{body}}
            </main>
            <script src="{{MermaidScriptUrl}}"></script>
            <script>
            {{Script.Replace("__MERMAID_URL__", MermaidScriptUrl)}}
            </script>
            </body>
            </html>
            """;
    }

    private const string Css = """
        :root { color-scheme: light; }
        body { margin: 0; font-family: Segoe UI, system-ui, sans-serif; font-size: 15px; line-height: 1.5; color: #1f2328; background: #ffffff; }
        main { max-width: 1400px; margin: 0 auto; padding: 16px 24px 64px; }
        nav { position: sticky; top: 0; background: #f6f8fa; border-bottom: 1px solid #d0d7de; padding: 8px 24px; font-size: 14px; }
        nav a { margin-right: 16px; }
        h1 { font-size: 1.8em; } h2 { font-size: 1.4em; margin-top: 2em; border-bottom: 1px solid #d0d7de; } h3 { font-size: 1.15em; margin-top: 1.6em; } h4 { font-size: 1em; margin-top: 1.4em; }
        table { border-collapse: collapse; margin: 12px 0; font-size: 14px; }
        th, td { border: 1px solid #d0d7de; padding: 4px 10px; text-align: left; vertical-align: top; }
        th { background: #f6f8fa; }
        code { background: #f6f8fa; padding: 1px 4px; border-radius: 4px; font-size: 0.9em; }
        pre.mermaid { background: transparent; overflow-x: auto; }
        pre.mermaid svg { max-width: 100%; height: auto; }
        .no-mermaid { background: #fff8c5; border: 1px solid #d4a72c; padding: 8px 12px; }
        """;

    // Markdig's diagram extension turns ```mermaid fences into <pre class="mermaid">, which is what Mermaid
    // renders in place. The body data attribute lets an automated check know when rendering finished.
    private const string Script = """
        (function () {
          var nav = document.getElementById('facilities');
          document.querySelectorAll('main h2').forEach(function (h) {
            if (!h.id) { return; }
            var a = document.createElement('a'); a.href = '#' + h.id; a.textContent = h.textContent; nav.appendChild(a);
          });
          if (typeof mermaid === 'undefined') {
            var note = document.createElement('p'); note.className = 'no-mermaid';
            note.textContent = 'Mermaid could not be loaded from __MERMAID_URL__, so the charts are shown as text.';
            document.querySelector('main').prepend(note);
            document.body.dataset.mermaidDone = 'unavailable';
            return;
          }
          mermaid.initialize({ startOnLoad: false, securityLevel: 'strict' });
          mermaid.run({ querySelector: 'pre.mermaid' })
            .then(function () { document.body.dataset.mermaidDone = 'ok'; })
            .catch(function (e) { document.body.dataset.mermaidDone = 'error: ' + (e && e.message ? e.message : e); });
        })();
        """;
}
