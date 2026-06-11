using HtmlAgilityPack;

namespace atfbs;

public class parser
{
    public HtmlDocument parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    public string? get_text(HtmlNode? node) =>
        node?.InnerText.Trim();
}