using System.Text;
using Newtonsoft.Json;

namespace atfbs;

public class export
{
    private readonly cfg _cfg;

    public export(cfg c) { _cfg = c; mkdirs(); }

    private void mkdirs()
    {
        Directory.CreateDirectory(_cfg.out_dir);
        Directory.CreateDirectory($"{_cfg.out_dir}/json");
        Directory.CreateDirectory($"{_cfg.out_dir}/csv");
    }

    public void write_json(Dictionary<string, List<post>> by_site)
    {
        if (!_cfg.json) return;
        foreach (var (site, posts) in by_site)
        {
            if (posts.Count == 0) continue;
            var p = $"{_cfg.out_dir}/json/{site}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            File.WriteAllText(p, JsonConvert.SerializeObject(posts, Formatting.Indented), Encoding.UTF8);
            Console.WriteLine($"  json -> {p}");
        }
    }

    public void write_csv(Dictionary<string, List<post>> by_site)
    {
        if (!_cfg.csv) return;
        foreach (var (site, posts) in by_site)
        {
            if (posts.Count == 0) continue;
            var p = $"{_cfg.out_dir}/csv/{site}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            using var w = new StreamWriter(p, false, Encoding.UTF8);
            w.WriteLine("title,site,url,pid,content,clen,scraped");
            foreach (var x in posts)
                w.WriteLine(string.Join(",",
                    esc(x.title), esc(x.site), esc(x.url),
                    esc(x.pid ?? ""), esc(x.content), x.clen, x.scraped.ToString("O")));
            Console.WriteLine($"  csv -> {p}");
        }
    }

    public void flush(Dictionary<string, List<post>> by_site)
    {
        var flat = by_site.Values.SelectMany(p => p).ToList();
        if (flat.Count == 0) { Console.WriteLine("  no posts"); return; }
        Console.WriteLine($"  exporting {flat.Count} posts...");
        write_json(by_site);
        write_csv(by_site);
        // combined
        if (_cfg.json)
        {
            var p = $"{_cfg.out_dir}/json/all_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            File.WriteAllText(p, JsonConvert.SerializeObject(flat, Formatting.Indented), Encoding.UTF8);
            Console.WriteLine($"  json -> {p}");
        }
        if (_cfg.csv)
        {
            var p = $"{_cfg.out_dir}/csv/all_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            using var w = new StreamWriter(p, false, Encoding.UTF8);
            w.WriteLine("title,site,url,pid,content,clen,scraped");
            foreach (var x in flat)
                w.WriteLine(string.Join(",",
                    esc(x.title), esc(x.site), esc(x.url),
                    esc(x.pid ?? ""), esc(x.content), x.clen, x.scraped.ToString("O")));
            Console.WriteLine($"  csv -> {p}");
        }
    }

    private static string esc(string v)
    {
        if (string.IsNullOrEmpty(v)) return "\"\"";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}