using System;
using System.IO;
using System.Text.Json;

namespace ZaapLauncher.Core.Services;

public static class LinkConfigService
{
    private const string FallbackDiscordUrl = "https://discord.gg/eGGu9ZVCG2";

    private static readonly Lazy<LinkSettings> Settings = new(LoadSettings);

    public static string DiscordInviteUrl =>
        string.IsNullOrWhiteSpace(Settings.Value.DiscordInviteUrl)
            ? FallbackDiscordUrl
            : Settings.Value.DiscordInviteUrl.Trim();

    private static LinkSettings LoadSettings()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "config", "links.json");
            if (!File.Exists(path))
                return new LinkSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LinkSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LinkSettings();
        }
        catch
        {
            return new LinkSettings();
        }
    }

    private sealed class LinkSettings
    {
        public string DiscordInviteUrl { get; set; } = string.Empty;
    }
}