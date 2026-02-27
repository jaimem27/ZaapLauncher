using System.Text.Json.Serialization;

namespace ZaapLauncher.Core.Models;

public sealed class NewsRoot
{
    [JsonPropertyName("items")]
    public List<NewsItem> Items { get; set; } = new();
}

public sealed class NewsItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("link")]
    public string Link { get; set; } = "";
}