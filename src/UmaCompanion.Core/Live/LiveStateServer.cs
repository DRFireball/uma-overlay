using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace UmaCompanion.Core.Live;

/// <summary>
/// Minimal loopback WebSocket server that the game hook connects to. Reads text frames, parses
/// them via <see cref="FrameParser"/>, and invokes a callback for each recognised frame.
/// Implemented over <see cref="TcpListener"/> so it runs cross-platform (no HTTP.sys dependency).
/// </summary>
public sealed class LiveStateServer : IDisposable
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener;
    private readonly Action<GameFrame> _onFrame;
    private readonly CancellationTokenSource _cts = new();

    public LiveStateServer(int port, Action<GameFrame> onFrame)
    {
        _onFrame = onFrame;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    /// <summary>The actual bound port (useful when constructed with port 0).</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                if (!await TryHandshakeAsync(stream, ct))
                {
                    return;
                }

                using var ws = WebSocket.CreateFromStream(
                    stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
                await ReadLoopAsync(ws, ct);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A misbehaving hook must not take the server down; drop this connection and keep listening.
        }
    }

    private async Task ReadLoopAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    return;
                }
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            var frame = FrameParser.Parse(json);
            if (frame is not null)
            {
                _onFrame(frame);
            }
        }
    }

    private static async Task<bool> TryHandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        var request = await ReadHttpHeadersAsync(stream, ct);
        var key = FindHeader(request, "Sec-WebSocket-Key");
        if (key is null)
        {
            return false;
        }

        var accept = Convert.ToBase64String(
            SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
        var bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes, ct);
        return true;
    }

    private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buf = new byte[1];
        while (!sb.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buf, ct);
            if (read == 0)
            {
                break;
            }
            sb.Append((char)buf[0]);
        }
        return sb.ToString();
    }

    private static string? FindHeader(string request, string name)
    {
        foreach (var line in request.Split("\r\n"))
        {
            var idx = line.IndexOf(':');
            if (idx > 0 && line[..idx].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(idx + 1)..].Trim();
            }
        }
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
