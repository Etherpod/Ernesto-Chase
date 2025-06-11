using Newtonsoft.Json;

namespace ErnestoChase;

public record RandomBool
{
    [JsonRequired]
    [JsonProperty("chance")]
    public float Chance;
}

public record RandomFloat
{
    [JsonRequired]
    [JsonProperty("min")]
    public float Min;
    [JsonRequired]
    [JsonProperty("max")]
    public float Max;
    [JsonRequired]
    [JsonProperty("chance")]
    public float Chance;
}

public record RandomOption
{
    [JsonRequired]
    [JsonProperty("options")]
    public string[] Options;
    [JsonRequired]
    [JsonProperty("chance")]
    public float Chance;
}