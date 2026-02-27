using System.Windows;

namespace ZaapLauncher.App.ViewModels;

public sealed class NewsItemVm
{
    public string Id { get; init; } = "";
    public string Date { get; init; } = "";
    public string Title { get; init; } = "";
    public string Tag { get; init; } = "";
    public string Body { get; init; } = "";
    public string Image { get; init; } = "";
    public string Link { get; init; } = "";

    public string ImageSource => string.IsNullOrWhiteSpace(Image) ? "" : Image;
    public Visibility HasImage => string.IsNullOrWhiteSpace(Image) ? Visibility.Collapsed : Visibility.Visible;
}