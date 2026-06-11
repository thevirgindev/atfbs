using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace atfbs;

public class scrap : IDisposable
{
    private readonly string _name;
    private readonly string _base;
    private readonly cfg _cfg;
    private readonly site_info _info;
    private readonly HttpClient _hc;
    private readonly Random _rng = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _last = DateTime.MinValue;

    public scrap(string name, site_info info, cfg c)
    {
        _name = name;
        _base = info.base_url;
        _info = info;
        _cfg = c;

        var h = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        if (!string.IsNullOrEmpty(c.proxy))
        {
            h.Proxy = new WebProxy(c.proxy);
            h.UseProxy = true;
        }

        _hc = new HttpClient(h);
        _hc.Timeout = TimeSpan.FromSeconds(c.timeout);
        _hc.DefaultRequestHeaders.Add("Accept", "text/html,*/*;q=0.8");
        _hc.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        rot_ua();
    }

    private void rot_ua()
    {
        var ua = cst.user_agents[_rng.Next(cst.user_agents.Length)];
        _hc.DefaultRequestHeaders.Remove("User-Agent");
        _hc.DefaultRequestHeaders.Add("User-Agent", ua);
    }

    private async Task rate_lim()
    {
        if (_cfg.rate_lim <= 0) return;
        await _gate.WaitAsync();
        try
        {
            var d = TimeSpan.FromSeconds(_cfg.rate_lim) - (DateTime.UtcNow - _last);
            if (d > TimeSpan.Zero) await Task.Delay(d);
            _last = DateTime.UtcNow;
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> fetch(string url, int try_n = 0)
    {
        await rate_lim();
        try
        {
            var r = await _hc.GetAsync(url);
            if (r.IsSuccessStatusCode) return await r.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException) when (try_n < _cfg.retries)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, try_n)));
            return await fetch(url, try_n + 1);
        }
        catch (TaskCanceledException) when (try_n < _cfg.retries)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, try_n)));
            return await fetch(url, try_n + 1);
        }
        return null;
    }

    public async Task<string?> fetch_tcp()
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("termbin.com", 9999);
            using var s = tcp.GetStream();
            var buf = new byte[65536];
            var n = await s.ReadAsync(buf);
            return Encoding.UTF8.GetString(buf, 0, n);
        }
        catch { return null; }
    }

    public string? extract(HtmlDocument doc)
    {
        foreach (var xp in _info.xpath_selectors)
        {
            try
            {
                var node = doc.DocumentNode.SelectSingleNode(xp);
                if (node != null)
                {
                    var t = node.InnerText.Trim();
                    if (t.Length > 0) return t;
                }
            }
            catch { continue; }
        }
        return doc.DocumentNode.SelectSingleNode("//body")?.InnerText.Trim();
    }

    public List<string> get_links(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var pat = new Regex(_info.link_pat, RegexOptions.Compiled);
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links == null) return [];

        return links
            .Select(a => a.GetAttributeValue("href", "").Trim())
            .Where(h => pat.IsMatch(h))
            .Select(h => h.StartsWith("http") ? h : _base.TrimEnd('/') + "/" + h.TrimStart('/'))
            .Distinct()
            .ToList();
    }

    public async Task<List<post>> scrape_all()
    {
        if (_info.is_tcp)
        {
            var c = await fetch_tcp();
            if (c == null) return [];
            return [new post
            {
                title = $"{_name} tcp",
                site = _name,
                url = $"{_name}:tcp",
                pid = DateTime.UtcNow.Ticks.ToString(),
                content = c,
                clen = c.Length,
                scraped = DateTime.UtcNow
            }];
        }

        var html = await fetch(_base);
        if (html == null) return [];

        var links = get_links(html);

        if (links.Count == 0)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var c = extract(doc);
            if (c == null || c.Length < _cfg.min_len) return [];
            return [new post
            {
                title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? _name,
                site = _name,
                url = _base,
                pid = _name,
                content = c,
                clen = c.Length,
                scraped = DateTime.UtcNow
            }];
        }

        var posts = new List<post>();
        var gate = new SemaphoreSlim(_info.max_conc, _info.max_conc);
        var tasks = links.Select(async link =>
        {
            await gate.WaitAsync();
            try
            {
                var h = await fetch(link);
                if (h == null) return;
                var d = new HtmlDocument();
                d.LoadHtml(h);
                var c = extract(d);
                if (c == null || c.Length < _cfg.min_len) return;
                var t = d.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? link;
                lock (posts)
                    posts.Add(new post
                    {
                        title = t,
                        site = _name,
                        url = link,
                        pid = link.Split('/').Last().Split('?').Last(),
                        content = c,
                        clen = c.Length,
                        scraped = DateTime.UtcNow
                    });
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);

        return posts;
    }

    public void Dispose()
    {
        _hc.Dispose();
        _gate.Dispose();
    }
}