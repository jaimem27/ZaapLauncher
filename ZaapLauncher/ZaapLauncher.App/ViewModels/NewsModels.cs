using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZaapLauncher.App.Class;

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
    public string? Image { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    // Helpers para XAML
    [JsonIgnore]
    public string DateText => Date;

    [JsonIgnore]
    public string ImageSource => string.IsNullOrWhiteSpace(Image) ? "" : Image;

    [JsonIgnore]
    public System.Windows.Visibility HasImage =>
        string.IsNullOrWhiteSpace(Image) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    [JsonIgnore]
    public System.Windows.Visibility HasLink =>
        string.IsNullOrWhiteSpace(Link) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    [JsonIgnore]
    public string LinkText => "Leer más →";
}
