namespace atfbs;

public record site_info(
    string name,
    string base_url,
    string link_pat,
    string[] xpath_selectors,
    bool needs_cookies = false,
    bool is_tcp = false,
    int max_conc = 3,
    string desc = ""
);

public static class cst
{
    public static readonly Dictionary<string, site_info> sites = new()
    {
        ["doxbin"] = new("doxbin", "https://doxbin.org",
            @"^/[a-zA-Z0-9_-]{6,24}$",
            ["//pre", "//code", "//div[contains(@class,'paste')]"],
            desc: "popular dox pastebin, often mirrors vilebin"),

        ["vilebin"] = new("vilebin", "https://vilebin.net",
            @"^/[a-zA-Z0-9]{6,16}$",
            ["//pre", "//code", "//div[contains(@class,'paste')]"],
            desc: "dox & leak pastebin, currently somewhat dead"),

        ["paste-ee"] = new("paste-ee", "https://paste.ee",
            @"^/p/[a-zA-Z0-9]+$",
            ["//div[@id='paste']", "//pre", "//code"],
            desc: "general pastebin, wide variety of content"),

        ["guns-lol"] = new("guns-lol", "https://guns.lol",
            @"^/[a-zA-Z0-9_-]{3,32}$",
            ["//div[contains(@class,'content')]", "//pre"],
            desc: "link-in-bio service, sometimes hosts configs"),

        ["dpaste"] = new("dpaste", "https://dpaste.com",
            @"^/[a-zA-Z0-9]{6,12}/?$",
            ["//textarea", "//pre", "//code"],
            desc: "django pastebin, code snippets & configs"),

        ["zerobin"] = new("zerobin", "https://zerobin.net",
            @"^\?[a-zA-Z0-9]+$",
            ["//pre", "//code"],
            desc: "encrypted pastebin, js-decrypted content"),

        ["privatebin"] = new("privatebin", "https://privatebin.com",
            @"^\?[a-zA-Z0-9]+$",
            ["//pre", "//code"],
            desc: "zerobin fork, encrypted pastes"),

        ["ghostbin"] = new("ghostbin", "https://ghostbin.co",
            @"^/[a-zA-Z0-9]{6,16}$",
            ["//pre", "//code", "//div[contains(@class,'paste')]"],
            desc: "lightweight pastebin, code & text"),

        ["hastebin"] = new("hastebin", "https://hastebin.com",
            @"^/share/[a-zA-Z0-9]+$",
            ["//pre", "//code", "//div[contains(@class,'content')]"],
            desc: "open source pastebin, often hosts logs"),

        ["termbin"] = new("termbin", "terminal",
            @"^.*$",
            [],
            is_tcp: true,
            max_conc: 1,
            desc: "tcp-based pastebin (port 9999), terminal output"),

        ["rentry"] = new("rentry", "https://rentry.co",
            @"^/[a-zA-Z0-9_-]{3,32}$",
            ["//pre", "//code"],
            desc: "markdown pastebin, essays & writeups"),

        ["paste-bin"] = new("paste-bin", "https://paste-bin.xyz",
            @"^/[a-zA-Z0-9]{8,32}$",
            ["//pre", "//code", "//textarea"],
            desc: "anonymous pastebin, code & configs"),

        ["slexy"] = new("slexy", "https://slexy.org",
            @"^/view/[a-zA-Z0-9]+$",
            ["//pre", "//code", "//div[contains(@class,'paste')]"],
            desc: "code snippet sharing"),

        ["pastecord"] = new("pastecord", "https://pastecord.com",
            @"^/[a-zA-Z0-9]{6,16}$",
            ["//pre", "//code"],
            desc: "pastebin for discord users"),

        ["ivpaste"] = new("ivpaste", "https://ivpaste.com",
            @"^/[a-zA-Z0-9]{6,16}$",
            ["//pre", "//code", "//textarea"],
            desc: "lightweight pastebin"),

        ["pst.azome"] = new("pst.azome", "https://pst.azome.net",
            @"^/[a-zA-Z0-9_-]{4,16}$",
            ["//pre", "//code"],
            desc: "minimal paste hosting"),

        ["paste.gg"] = new("paste.gg", "https://paste.gg",
            @"^/p/[a-zA-Z0-9_-]{8,32}$",
            ["//pre", "//code", "//div[contains(@class,'content')]"],
            desc: "modern pastebin, good uptime"),

        ["neocities"] = new("neocities", "https://neocities.org",
            @"^/site/[a-zA-Z0-9_-]{2,32}$",
            ["//pre", "//code", "//textarea"],
            desc: "static web hosting, sometimes leaks"),

        ["paste.sh"] = new("paste.sh", "https://paste.sh",
            @"^/[a-zA-Z0-9_-]{6,32}$",
            ["//pre", "//code"],
            desc: "encrypted pastebin, terminal-friendly"),
    };

    public static readonly string[] all_sites = sites.Keys.ToArray();

    public static readonly string[] user_agents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/121.0.0.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/17.2",
    ];
}