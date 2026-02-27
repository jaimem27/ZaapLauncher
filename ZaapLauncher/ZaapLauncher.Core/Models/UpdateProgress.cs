namespace ZaapLauncher.Core.Models;

public enum UpdateStage
{
    FetchManifest,
    VerifyFiles,
    Downloading,
    Applying,
    FinalCheck,
    Ready
}

public sealed record UpdateProgress(
    UpdateStage Stage,
    double Percent,
    string Headline,
    string Detail
);