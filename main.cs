using System.Text;
using Spectre.Console;

namespace atfbs;

static class banner
{
    public static string art = @"
    █████████   ███████████ ███████████ ███████████   █████████
   ███▒▒▒▒▒███ ▒█▒▒▒███▒▒▒█▒▒███▒▒▒▒▒▒█▒▒███▒▒▒▒▒███ ███▒▒▒▒▒███
  ▒███    ▒███ ▒   ▒███  ▒  ▒███   █ ▒  ▒███    ▒███▒███    ▒▒▒
  ▒███████████     ▒███     ▒███████    ▒██████████ ▒▒█████████
  ▒███▒▒▒▒▒███     ▒███     ▒███▒▒▒█    ▒███▒▒▒▒▒███ ▒▒▒▒▒▒▒▒███
  ▒███    ▒███     ▒███     ▒███  ▒     ▒███    ▒███ ███    ▒███
  █████   █████    █████    █████       ███████████ ▒▒█████████
  ▒▒▒▒▒   ▒▒▒▒▒    ▒▒▒▒▒    ▒▒▒▒▒       ▒▒▒▒▒▒▒▒▒▒▒   ▒▒▒▒▒▒▒▒▒";
}

public class main
{
    private static tui_state _state = new();

    public static async Task<int> run(string[] args_raw)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var args = args_raw.ToList();
        if (args.Count > 0 && args[0] == "run") args = args.Skip(1).ToList();

        // help
        if (args.Contains("--help") || args.Contains("-h") || args.Contains("-?") || args.Contains("/?"))
        {
            cli_help(); return 0;
        }

        if (args.Count == 0)
        {
            var _cfg = cfg.load();
            _state = new tui_state { cfg = _cfg };
            await tui_loop();
            return 0;
        }

        // parse flags
        var _cfg2 = cfg.load();
        var sites = new List<string>();
        var keyword = "";
        var phase = args.Contains("--phase") || args.Contains("-p");
        var reset = args.Contains("--reset") || args.Contains("-r");

        for (int i = 0; i < args.Count; i++)
        {
            if ((args[i] == "--sites" || args[i] == "-s" || args[i] == "--from" || args[i] == "-f") && i + 1 < args.Count)
            {
                i++;
                sites.AddRange(args[i].Split(',').Select(s => s.Trim().ToLower()));
            }
            else if ((args[i] == "--search" || args[i] == "-q") && i + 1 < args.Count)
            {
                i++;
                keyword = args[i].ToLower();
            }
            else if (args[i].StartsWith("--") || args[i].StartsWith("-"))
                continue;
            else
                sites.Add(args[i].ToLower());
        }

        sites = sites.Distinct().ToList();

        if (sites.Count == 0)
        {
            cli_help(); return 1;
        }

        foreach (var s in sites)
        {
            if (keyword != "")
                Console.WriteLine($"  searching for '{keyword}' in {s}");
            if (phase)
                Console.WriteLine($"  phase mode: scraping next batch from {s}");
            if (reset)
                Console.WriteLine($"  resetting phase progress for {s}");
        }

        return await cli_run(_cfg2, sites, keyword, phase, reset);
    }

    private class tui_state
    {
        public cfg cfg = new();
        public int total_scraped = 0;
        public int sites_ok = 0;
        public string last_run = "";
        public bool running = false;
        public Dictionary<string, int> results = new();
        public Dictionary<string, string> errors = new();
        public int last_w = 0;
    }

    // ── PHASE TRACKING ──
    private static string phase_file(string site)
    {
        var dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        return Path.Combine(dir, $".phase_{site}");
    }

    private static List<string> load_phase(string site)
    {
        var f = phase_file(site);
        if (!File.Exists(f)) return [];
        return [.. File.ReadAllLines(f).Where(l => l.Trim() != "")];
    }

    private static void save_phase(string site, List<string> done)
    {
        File.WriteAllLines(phase_file(site), done.Distinct());
    }

    private static void clear_phase(string site)
    {
        var f = phase_file(site);
        if (File.Exists(f)) File.Delete(f);
    }

    // ── CLI MODE ──
    private static int term_width()
    {
        try { return Console.WindowWidth; }
        catch { return 60; }
    }

    private static async Task<int> cli_run(cfg _cfg, List<string> sites, string keyword, bool phase, bool reset)
    {
        var sep = new string('─', Math.Max(20, term_width() - 1));
        AnsiConsole.MarkupLine($"[bold yellow]{sep}[/]");
        AnsiConsole.MarkupLine($"[bold yellow]ATFBS v2.1[/]  [grey]all the fucking bin scrapers[/]  [dim]by @thevirgindev[/]");
        AnsiConsole.MarkupLine($"[bold yellow]{sep}[/]");
        Console.WriteLine();

        var by_site = new Dictionary<string, List<post>>();
        var scrapers = new List<scrap>();
        var gate = new SemaphoreSlim(_cfg.max_conc, _cfg.max_conc);

        foreach (var s in sites) by_site[s] = [];
        if (reset) foreach (var s in sites) clear_phase(s);

        var jobs = sites.Select(async site =>
        {
            await gate.WaitAsync();
            try
            {
                if (!cst.sites.TryGetValue(site, out var info))
                { AnsiConsole.MarkupLine($"  [red]!![/] unknown site: [white]{site}[/]"); return; }

                var s = new scrap(site, info, _cfg);
                lock (scrapers) scrapers.Add(s);

                var posts = await s.scrape_all();

                // phase: skip already-done links
                if (phase)
                {
                    var done = load_phase(site);
                    var undone = posts.Where(p => !done.Contains(p.url)).ToList();
                    var batch = undone.Take(_cfg.max_pages).ToList();
                    if (batch.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]!![/] [cyan]{site}[/]: all links scraped. use --reset to start over.");
                        return;
                    }
                    // only fetch the batch
                    batch = await fetch_batch(batch, site, info, _cfg, s);
                    save_phase(site, done.Concat(batch.Select(p => p.url)).ToList());

                    if (keyword != "")
                        batch = batch.Where(p => p.title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || p.content.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

                    lock (by_site) by_site[site] = batch;
                    var remaining = undone.Count - batch.Count;
                    var total_links = posts.Count;
                    var pc = total_links > 0 ? (load_phase(site).Count * 100 / total_links) : 0;
                    AnsiConsole.MarkupLine($"  [green]ok[/]  [cyan]{site}[/]: [white]{batch.Count}[/] posts (phase {pc}%)");
                    if (remaining > 0)
                        AnsiConsole.MarkupLine($"  [grey]   {remaining} links remaining[/]");
                }
                else
                {
                    var batch = posts.Take(_cfg.max_pages).ToList();
                    if (keyword != "")
                        batch = batch.Where(p => p.title.Contains(keyword, StringComparison.OrdinalIgnoreCase) || p.content.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
                    lock (by_site) by_site[site] = batch;
                    AnsiConsole.MarkupLine($"  [green]ok[/]  [cyan]{site}[/]: [white]{batch.Count}[/] posts");
                }
            }
            catch (Exception ex)
            { AnsiConsole.MarkupLine($"  [red]!![/] [cyan]{site}[/]: [grey]{ex.Message}[/]"); }
            finally { gate.Release(); }
        });

        await Task.WhenAll(jobs);

        var exp = new export(_cfg);
        exp.flush(by_site);

        var total = by_site.Values.Sum(p => p.Count);
        var ok = by_site.Count(kv => kv.Value.Count > 0);
        AnsiConsole.MarkupLine($"\n[green]ok[/]  done: [cyan]{ok}[/]/[white]{sites.Count}[/] sites, [cyan]{total}[/] posts");

        foreach (var s in scrapers) s.Dispose();
        return 0;
    }

    private static async Task<List<post>> fetch_batch(List<post> stub, string site, site_info info, cfg _cfg, scrap s)
    {
        // stub posts only have a url. re-fetch each properly
        var posts = new List<post>();
        var gate = new SemaphoreSlim(info.max_conc, info.max_conc);
        var jobs = stub.Select(async p =>
        {
            await gate.WaitAsync();
            try
            {
                var h = await s.fetch(p.url);
                if (h == null) return;
                var d = new HtmlAgilityPack.HtmlDocument();
                d.LoadHtml(h);
                var c = s.extract(d);
                if (c == null || c.Length < _cfg.min_len) return;
                var t = d.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? p.url;
                lock (posts)
                    posts.Add(new post
                    {
                        title = t,
                        site = site,
                        url = p.url,
                        pid = p.url.Split('/').Last().Split('?').Last(),
                        content = c,
                        clen = c.Length,
                        scraped = DateTime.UtcNow
                    });
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(jobs);
        return posts;
    }

    private static void cli_help()
    {
        var w = term_width();
        var sep = new string('─', Math.Max(20, w - 1));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[bold yellow]{sep}[/]");
        sb.AppendLine("[bold yellow]ATFBS v2.1[/]  [grey]all the fucking bin scrapers[/]  [dim]by @thevirgindev[/]");
        sb.AppendLine($"[bold yellow]{sep}[/]");
        sb.AppendLine("");
        sb.AppendLine("  [grey]commands:[/]");
        sb.AppendLine("    [cyan]atfbs[/]                          [grey]interactive dashboard[/]");
        sb.AppendLine("    [cyan]atfbs <sites..>[/]                [grey]scrape sites by name[/]");
        sb.AppendLine("    [cyan]atfbs -s a,b,c[/]                 [grey]scrape comma-separated[/]");
        sb.AppendLine("    [cyan]atfbs --from s1,s2[/]             [grey]same as -s[/]");
        sb.AppendLine("    [cyan]atfbs -q <keyword>[/]             [grey]filter by keyword[/]");
        sb.AppendLine("    [cyan]atfbs --phase[/]                  [grey]scrape next batch[/]");
        sb.AppendLine("    [cyan]atfbs --phase --reset[/]          [grey]reset phase progress[/]");
        sb.AppendLine("    [cyan]atfbs --help[/]                   [grey]this[/]");
        sb.AppendLine("");
        sb.AppendLine("  [grey]cfg.json options: rate_limit, timeout, max_conc,");
        sb.AppendLine("  max_pages (default 30), min_len, proxy, retries[/]");
        sb.AppendLine("");
        sb.AppendLine("  [grey]sites (19 total):[/]");

        foreach (var s in cst.all_sites)
        {
            var info = cst.sites[s];
            var proto = info.is_tcp ? "[blue]tcp[/]" : "[green]http[/]";
            sb.AppendLine($"  [cyan]{s,-13}[/] {proto}  [grey]{info.desc}[/]");
        }

        sb.AppendLine("");
        sb.AppendLine("  [grey]examples:[/]");
        sb.AppendLine("    [cyan]atfbs doxbin vilebin[/]");
        sb.AppendLine("    [cyan]atfbs -s paste-ee,guns-lol -q email[/]");
        sb.AppendLine("    [cyan]atfbs doxbin --phase[/]");
        sb.AppendLine("    [cyan]atfbs --from rentry --phase --reset[/]");

        var panel = new Panel(Align.Center(new Markup(sb.ToString())))
            .Border(BoxBorder.None)
            .Padding(0, 0);
        AnsiConsole.Write(panel);
    }

    // ── TUI ──
    private static async Task tui_loop()
    {
        while (true)
        {
            if (_state.running)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots12)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .StartAsync("[yellow]scraping...[/]", async ctx =>
                    {
                        while (_state.running)
                        {
                            poll_resize();
                            await Task.Delay(300);
                        }
                    });
                continue;
            }

            // poll for resize before showing menu
            for (int i = 0; i < 30; i++)
            {
                if (poll_resize()) render_dashboard();
                await Task.Delay(100);
            }

            render_dashboard();

            var menu = new SelectionPrompt<string>();
            menu.Title("[bold]select action:[/]");
            menu.AddChoiceGroup(" [cyan]scraping[/]", [
                "  run all sites",
                "  run specific sites...",
            ]);
            menu.AddChoiceGroup(" [cyan]info[/]", [
                "  what is atfbs?",
                "  how to use this tool",
                "  how to profit from it",
            ]);
            menu.AddChoiceGroup(" [cyan]system[/]", [
                "  view config",
                "  view output directory",
                "  exit",
            ]);

            var choice = AnsiConsole.Prompt(menu);
            poll_resize();

            switch (choice)
            {
                case "  run all sites":
                    await do_scrape(cst.all_sites.ToList());
                    break;
                case "  run specific sites...":
                    var picked = AnsiConsole.Prompt(
                        new MultiSelectionPrompt<string>()
                            .Title("pick sites (space to toggle, enter to confirm)")
                            .InstructionsText("[grey](space = toggle, enter = confirm)[/]")
                            .AddChoices(cst.all_sites)
                    );
                    poll_resize();
                    if (picked.Count > 0) await do_scrape(picked);
                    break;
                case "  what is atfbs?":
                    info_panel("about atfbs", about_text());
                    break;
                case "  how to use this tool":
                    info_panel("how to use", usage_text());
                    break;
                case "  how to profit from it":
                    info_panel("profit strategies", profit_text());
                    break;
                case "  view config":
                    config_panel();
                    break;
                case "  view output directory":
                    output_panel();
                    break;
                case "  exit":
                    AnsiConsole.MarkupLine("\n[grey]later, nerd.[/]");
                    return;
            }
        }
    }

    private static bool poll_resize()
    {
        int w;
        try { w = Console.WindowWidth; }
        catch { w = 60; }
        if (w != _state.last_w)
        {
            _state.last_w = w;
            return true;
        }
        return false;
    }

    private static void render_dashboard()
    {
        int w;
        try { w = Console.WindowWidth; }
        catch { w = 60; }
        var site_col = Math.Max(8, w / 6);
        var proto_col = 6;
        var posts_col = 6;
        var info_col = w - site_col - proto_col - posts_col - 8;

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Blue);
        table.AddColumn(new TableColumn("[yellow]site[/]").LeftAligned().Width(site_col));
        table.AddColumn(new TableColumn("[yellow]proto[/]").Centered().Width(proto_col));
        table.AddColumn(new TableColumn("[yellow]posts[/]").Centered().Width(posts_col));
        table.AddColumn(new TableColumn("[yellow]info[/]").LeftAligned().Width(info_col));

        foreach (var s in cst.all_sites)
        {
            var info = cst.sites[s];
            var ok = _state.results.GetValueOrDefault(s, 0);
            var err = _state.errors.GetValueOrDefault(s, "");
            var proto = info.is_tcp ? "[blue]tcp[/]" : "[green]http[/]";
            var cnt = ok > 0 ? $"[green]{ok}[/]" : "[grey]-[/]";
            var note = err != "" ? "[red]err[/]" :
                       ok > 0 ? $"[green]{ok} posts[/]" :
                       "[grey]not scraped[/]";
            table.AddRow($"[cyan]{s}[/]", proto, cnt, note);
        }

        var stats = new Panel(
            Align.Center(new Markup(
                $"[bold]scraped:[/] [cyan]{_state.total_scraped}[/]  " +
                $"[bold]sites ok:[/] [green]{_state.sites_ok}[/]/[white]{cst.all_sites.Length}[/]  " +
                $"[bold]last run:[/] [grey]{(string.IsNullOrEmpty(_state.last_run) ? "never" : _state.last_run)}[/]"
            ))
        ).Border(BoxBorder.Rounded).BorderColor(Color.Blue);

        var layout = new Rows(table, new Text(""), stats);

        AnsiConsole.Clear();
        var panel = new Panel(layout)
            .Border(BoxBorder.Double)
            .BorderColor(Color.Yellow)
            .Header("[bold yellow]ATFBS v2.1[/]  [dim]by @thevirgindev[/]")
            .Padding(1, 0);
        AnsiConsole.Write(panel);
    }

    // ── info panels ──
    private static void info_panel(string title, string text)
    {
        AnsiConsole.Clear();
        var lines = text.Split('\n');
        // render as plain text, no markup
        foreach (var line in lines)
            AnsiConsole.WriteLine(line);
        AnsiConsole.MarkupLine("\n[grey]press any key to return...[/]");
        Console.ReadKey(true);
        poll_resize();
    }

    private static void config_panel()
    {
        AnsiConsole.Clear();
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_state.cfg, Newtonsoft.Json.Formatting.Indented);
        // render plain, not through markup
        foreach (var line in json.Split('\n'))
            AnsiConsole.WriteLine(line);
        AnsiConsole.MarkupLine("\n[grey]press any key to return...[/]");
        Console.ReadKey(true);
        poll_resize();
    }

    private static void output_panel()
    {
        AnsiConsole.Clear();
        var lines = new List<string>();
        var dir = _state.cfg.out_dir;

        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles($"{dir}/json", "*.json")
                .Concat(Directory.GetFiles($"{dir}/csv", "*.csv"))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(30)
                .ToList();

            if (files.Count == 0)
                AnsiConsole.WriteLine("  no output files yet. run a scrape first.");
            else
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    var size = fi.Length switch
                    {
                        < 1024 => $"{fi.Length}B",
                        < 1048576 => $"{fi.Length / 1024}KB",
                        _ => $"{fi.Length / 1048576}MB"
                    };
                    var rel = Path.GetRelativePath(dir, f);
                    AnsiConsole.WriteLine($"  {rel,-40} {size,8}  {fi.LastWriteTime:HH:mm:ss}");
                }
        }
        else
            AnsiConsole.WriteLine($"  directory '{dir}' does not exist");

        AnsiConsole.MarkupLine("\n[grey]press any key to return...[/]");
        Console.ReadKey(true);
        poll_resize();
    }

    // ── scrape ──
    private static async Task do_scrape(List<string> sites)
    {
        _state.running = true;
        _state.last_run = DateTime.Now.ToString("HH:mm:ss");
        _state.results = sites.ToDictionary(s => s, _ => 0);
        foreach (var s in sites) _state.errors.Remove(s);
        poll_resize();

        var by_site = new Dictionary<string, List<post>>();
        var scrapers = new List<scrap>();
        var gate = new SemaphoreSlim(_state.cfg.max_conc, _state.cfg.max_conc);

        foreach (var s in sites) by_site[s] = [];

        var jobs = sites.Select(async site =>
        {
            await gate.WaitAsync();
            try
            {
                if (!cst.sites.TryGetValue(site, out var info))
                { _state.errors[site] = "unknown"; return; }
                var s = new scrap(site, info, _state.cfg);
                lock (scrapers) scrapers.Add(s);
                try
                {
                    var posts = await s.scrape_all();
                    var batch = posts.Take(_state.cfg.max_pages).ToList();
                    lock (by_site) by_site[site] = batch;
                    _state.results[site] = batch.Count;
                }
                catch (Exception ex)
                { _state.errors[site] = ex.Message; }
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(jobs);

        var exp = new export(_state.cfg);
        exp.flush(by_site);

        _state.total_scraped += by_site.Values.Sum(p => p.Count);
        _state.sites_ok += by_site.Count(kv => kv.Value.Count > 0);
        foreach (var s in scrapers) s.Dispose();
        _state.running = false;
        _state.last_w = 0;
    }

    // ── text content ──
    private static string about_text() => @"
ATFBS - All The Fucking Bin Scrapers

A pastebin intelligence tool. Scrapes 19 different
pastebin-style sites, extracts content using per-site
strategies, and exports to JSON and CSV.

Pastebins are where people dump things they found but
don't know what to do with. Credentials, config files,
internal IPs, API keys, slack tokens, aws secrets -
everything ends up in a paste at some point.

ATFBS scrapes recent pastes from 19 known pastebin
services and saves them for analysis. Every site gets
its own URL pattern matcher and content extraction
strategy. Termbin connects over TCP. Others use HTTP
with exponential backoff retry and rotating user agents.

Use cases:
  - credential harvesting from leaked configs
  - osint recon for pentests
  - threat intelligence feed aggregation
  - data breach documentation for journalism
  - unindexed data collection and brokerage

Output lands in output/ - JSON for programmatic use,
CSV for spreadsheets and filtering.";

    private static string usage_text() => @"
usage:
  atfbs                         interactive dashboard
  atfbs <sites..>               scrape specific sites
  atfbs -s a,b,c                comma-separated sites
  atfbs --from doxbin,vilebin   same as -s
  atfbs -q password             filter results by keyword
  atfbs --phase                 scrape next batch
  atfbs --phase --reset         reset phase progress

cfg.json options:
  rate_limit    delay between requests (seconds)
  timeout       HTTP timeout (seconds)
  max_conc      how many sites to scrape at once
  max_pages     links per phase (default 30)
  min_len       skip posts shorter than this
  proxy         HTTP or SOCKS proxy URL
  retries       retry attempts on failure

output:
  output/json/*.json      pretty-printed JSON
  output/csv/*.csv         RFC 4180 CSV

Docker or subprocess:
  Exits with code 0 on success, 1 on error.
  Pipe output to your Discord bot or webhook.";

    private static string profit_text() => @"
How to profit from ATFBS:

1. credential monitoring
   Scrape for email:password combos in leaked configs.
   Sell a service that alerts companies when their
   domain appears in a paste.

2. osint for pentests
   Internal IPs, API keys, slack tokens, aws secrets
   in paste dumps. Sell recon reports to companies.

3. threat intelligence feeds
   Automate the scraper, feed into a Discord bot,
   sell access to a private channel with real-time
   paste alerts filtered by keywords.

4. data brokerage
   Aggregate paste content over time. Sell curated
   datasets - doxbin dumps from 2025, paste-ee config
   files containing aws keys, etc.

5. journalism or research
   Data breach writeups get traffic. Use exports to
   spot trends before they make news.

Bottom line: pastebins are the unindexed underbelly
of the internet. ATFBS gives you a shovel.";
}