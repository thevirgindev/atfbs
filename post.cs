namespace atfbs;

public class post
{
    public string title { get; set; } = "";
    public string site { get; set; } = "";
    public string url { get; set; } = "";
    public string? pid { get; set; }
    public string content { get; set; } = "";
    public int clen { get; set; }
    public DateTime scraped { get; set; } = DateTime.UtcNow;
}