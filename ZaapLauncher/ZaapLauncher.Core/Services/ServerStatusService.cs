using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZaapLauncher.Core.Services;

public readonly record struct ServerStatusSnapshot(string Endpoint, bool IsOnline, string Detail);

public sealed class ServerStatusService
{
    private const string DefaultServerEndpoint = "127.0.0.1:444"; // Cambiar por tu IP:puerto por defecto
    private readonly TimeSpan _timeout;

    public ServerStatusService(string? endpoint = null, TimeSpan? timeout = null)
    {
        Endpoint = ResolveEndpoint(endpoint);
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public string Endpoint { get; }

    public async Task<ServerStatusSnapshot> GetStatusAsync(CancellationToken ct)
    {
        if (!TryParseEndpoint(Endpoint, out var host, out var port))
            return new ServerStatusSnapshot(Endpoint, false, "Formato inválido. Usa host:puerto.");

        try
        {
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(host, port, linkedCts.Token);

            return tcpClient.Connected
                ? new ServerStatusSnapshot(Endpoint, true, "Puerto abierto")
                : new ServerStatusSnapshot(Endpoint, false, "Sin conexión");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServerStatusSnapshot(Endpoint, false, "Timeout de conexión");
        }
        catch (SocketException ex)
        {
            return new ServerStatusSnapshot(Endpoint, false, ex.SocketErrorCode.ToString());
        }
        catch (Exception ex)
        {
            return new ServerStatusSnapshot(Endpoint, false, ex.GetType().Name);
        }
    }

    private static string ResolveEndpoint(string? endpoint)
    {
        var envEndpoint = Environment.GetEnvironmentVariable("ZAAP_SERVER_ENDPOINT");

        if (!string.IsNullOrWhiteSpace(envEndpoint))
            return envEndpoint.Trim();

        if (!string.IsNullOrWhiteSpace(endpoint))
            return endpoint.Trim();

        return DefaultServerEndpoint;
    }

    private static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var separatorIndex = endpoint.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= endpoint.Length - 1)
            return false;

        host = endpoint[..separatorIndex].Trim();
        var portRaw = endpoint[(separatorIndex + 1)..].Trim();

        return host.Length > 0
            && int.TryParse(portRaw, out port)
            && port is >= 1 and <= 65535;
    }
}