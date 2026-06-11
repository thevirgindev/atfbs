using Newtonsoft.Json;

namespace atfbs;

public class cfg
{
    [JsonProperty("rate_limit")]
    public double rate_lim { get; set; } = 0.5;

    [JsonProperty("timeout")]
    public int timeout { get; set; } = 30;

    [JsonProperty("max_conc")]
    public int max_conc { get; set; } = 5;

    [JsonProperty("out_dir")]
    public string out_dir { get; set; } = "output";

    [JsonProperty("sites")]
    public List<string> sites { get; set; } = ["doxbin", "vilebin", "paste-ee"];

    [JsonProperty("min_len")]
    public int min_len { get; set; } = 0;

    [JsonProperty("max_pages")]
    public int max_pages { get; set; } = 30;

    [JsonProperty("json")]
    public bool json { get; set; } = true;

    [JsonProperty("csv")]
    public bool csv { get; set; } = true;

    [JsonProperty("verb")]
    public bool verb { get; set; } = false;

    [JsonProperty("proxy")]
    public string? proxy { get; set; }

    [JsonProperty("retries")]
    public int retries { get; set; } = 3;

    public static cfg load(string path = "cfg.json")
    {
        try
        {
            if (File.Exists(path))
            {
                var j = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<cfg>(j) ?? new cfg();
            }
        }
        catch { }
        return new cfg();
    }

    public void save(string path = "cfg.json")
    {
        var j = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(path, j);
    }
}