# ATFBS — All The Fucking Bin Scrapers

```
    █████████   ███████████ ███████████ ███████████   █████████
   ███▒▒▒▒▒███ ▒█▒▒▒███▒▒▒█▒▒███▒▒▒▒▒▒█▒▒███▒▒▒▒▒███ ███▒▒▒▒▒███
  ▒███    ▒███ ▒   ▒███  ▒  ▒███   █ ▒  ▒███    ▒███▒███    ▒▒▒
  ▒███████████     ▒███     ▒███████    ▒██████████ ▒▒█████████
  ▒███▒▒▒▒▒███     ▒███     ▒███▒▒▒█    ▒███▒▒▒▒▒███ ▒▒▒▒▒▒▒▒███
  ▒███    ▒███     ▒███     ▒███  ▒     ▒███    ▒███ ███    ▒███
  █████   █████    █████    █████       ███████████ ▒▒█████████
  ▒▒▒▒▒   ▒▒▒▒▒    ▒▒▒▒▒    ▒▒▒▒▒       ▒▒▒▒▒▒▒▒▒▒▒   ▒▒▒▒▒▒▒▒▒
```

A pastebin intelligence tool. Scrapes 19 pastebin-style sites using per-site strategies and exports to JSON and CSV.

## Features

- **19 pastebin sites** — doxbin, vilebin, paste.ee, guns.lol, dpaste, zerobin, privatebin, ghostbin, hastebin, termbin, rentry, paste-bin, slexy, pastecord, ivpaste, pst.azome, paste.gg, neocities, paste.sh
- **Per-site strategies** — each site gets its own URL pattern matcher and content selectors
- **Concurrent scraping** — configurable max concurrency with rate limiting
- **Keyword search** — `-q` flag filters results by keyword in title and content
- **Phase system** — `--phase` scrapes batches of links, tracks progress in `.phase` files
- **Resizable TUI** — interactive dashboard with live stats, site table, and menu
- **CLI mode** — progress bars, colored output, pipe-friendly for subprocesses
- **Exports** — JSON and RFC 4180 CSV
- **Proxy support** — HTTP and SOCKS via cfg.json
- **Retry with backoff** — exponential backoff on HTTP failures

## Usage

```
atfbs                         interactive dashboard
atfbs doxbin vilebin          scrape sites by name
atfbs -s paste-ee,guns-lol    scrape comma-separated
atfbs -q "password"           filter results by keyword
atfbs --from rentry --phase   scrape next batch
atfbs --phase --reset         reset phase progress
atfbs --help                  show help
```

### Phase system

When a site has many links, `--phase` scrapes them in batches:

```
atfbs doxbin --phase          scrape first 30 links
atfbs doxbin --phase          scrape next 30 links
atfbs doxbin --phase          ...repeat until all done
atfbs doxbin --phase --reset  start over
```

Phase progress is stored in `.phase_<sitename>` files.

## Configuration

Create `cfg.json` in the executable directory:

```json
{
  "rate_limit": 0.5,
  "timeout": 30,
  "max_conc": 5,
  "max_pages": 30,
  "min_len": 0,
  "json": true,
  "csv": true,
  "proxy": null,
  "retries": 3
}
```

| Option | Default | Description |
|--------|---------|-------------|
| rate_limit | 0.5 | Delay between requests (seconds) |
| timeout | 30 | HTTP timeout (seconds) |
| max_conc | 5 | How many sites to scrape at once |
| max_pages | 30 | Links per phase batch |
| min_len | 0 | Skip posts shorter than this |
| proxy | null | HTTP or SOCKS proxy URL |
| retries | 3 | Retry attempts on failure |

## Build

```bash
dotnet build -c Release
```

The compiled binary is at `bin/Release/net10.0/atfbs.exe`. Can be run standalone or piped as a subprocess.

## Docker / Subprocess

Exit code 0 = success, 1 = error. Pipe output to a Discord bot, webhook, or log file.

```
atfbs doxbin rentry -q "api_key" --phase
```

## Sites

| Site | Protocol | Description |
|------|----------|-------------|
| doxbin | http | Popular dox pastebin, often mirrors vilebin |
| vilebin | http | Dox and leak pastebin |
| paste-ee | http | General pastebin |
| guns-lol | http | Link-in-bio service, sometimes hosts configs |
| dpaste | http | Django pastebin, code snippets |
| zerobin | http | Encrypted pastebin, JS-decrypted content |
| privatebin | http | Zerobin fork |
| ghostbin | http | Lightweight pastebin |
| hastebin | http | Open source pastebin, logs |
| termbin | tcp | TCP-based pastebin (port 9999) |
| rentry | http | Markdown pastebin, essays and writeups |
| paste-bin | http | Anonymous pastebin |
| slexy | http | Code snippet sharing |
| pastecord | http | Pastebin for Discord users |
| ivpaste | http | Lightweight pastebin |
| pst.azome | http | Minimal paste hosting |
| paste.gg | http | Modern pastebin |
| neocities | http | Static web hosting |
| paste.sh | http | Encrypted pastebin |

## License

MIT