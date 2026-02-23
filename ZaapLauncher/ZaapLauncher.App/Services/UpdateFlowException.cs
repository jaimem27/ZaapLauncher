using System;

namespace ZaapLauncher.App.Services;

public sealed class UpdateFlowException : Exception
{
    public UpdateFlowException(string headline, string detail, Exception? innerException = null)
        : base(detail, innerException)
    {
        Headline = headline;
        Detail = detail;
    }

    public string Headline { get; }
    public string Detail { get; }
}
