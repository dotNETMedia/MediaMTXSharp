using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MediaMtx.Net.Rtsp;

public sealed class RtspServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptTask;
    private int _sessionCounter;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public RtspServer(IPEndPoint endpoint)
    {
        _listener = new TcpListener(endpoint);
    }

    public void Start()
    {
        _listener.Start();
        _acceptTask = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        string? sessionId = null;

        while (!_cts.IsCancellationRequested && client.Connected)
        {
            var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
            if (requestLine is null)
            {
                break;
            }
            if (requestLine.Length == 0)
            {
                continue;
            }
            var parts = requestLine.Split(' ');
            if (parts.Length < 3)
            {
                break;
            }
            var method = parts[0];
            var cseq = "0";
            string? line;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
            {
                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var key = line[..idx].Trim();
                    var value = line[(idx + 1)..].Trim();
                    headers[key] = value;
                    if (key.Equals("CSeq", StringComparison.OrdinalIgnoreCase))
                    {
                        cseq = value;
                    }
                }
            }

            switch (method)
            {
                case "OPTIONS":
                    await writer.WriteLineAsync("RTSP/1.0 200 OK").ConfigureAwait(false);
                    await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                    await writer.WriteLineAsync("Public: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN").ConfigureAwait(false);
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    break;
                case "DESCRIBE":
                    const string sdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=No Name\r\nt=0 0\r\nm=video 0 RTP/AVP 96\r\na=control:streamid=0\r\na=rtpmap:96 H264/90000\r\n";
                    await writer.WriteLineAsync("RTSP/1.0 200 OK").ConfigureAwait(false);
                    await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                    await writer.WriteLineAsync("Content-Base: rtsp://127.0.0.1/").ConfigureAwait(false);
                    await writer.WriteLineAsync("Content-Type: application/sdp").ConfigureAwait(false);
                    await writer.WriteLineAsync($"Content-Length: {sdp.Length}").ConfigureAwait(false);
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    await writer.WriteAsync(sdp).ConfigureAwait(false);
                    break;
                case "SETUP":
                    sessionId ??= Interlocked.Increment(ref _sessionCounter).ToString();
                    var transport = headers.GetValueOrDefault("Transport", string.Empty);
                    if (transport.Contains("TCP", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("RTSP/1.0 200 OK").ConfigureAwait(false);
                        await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                        await writer.WriteLineAsync("Transport: RTP/AVP/TCP;unicast;interleaved=0-1").ConfigureAwait(false);
                        await writer.WriteLineAsync($"Session: {sessionId}").ConfigureAwait(false);
                        await writer.WriteLineAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await writer.WriteLineAsync("RTSP/1.0 461 Unsupported Transport").ConfigureAwait(false);
                        await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                        await writer.WriteLineAsync().ConfigureAwait(false);
                    }
                    break;
                case "PLAY":
                    await writer.WriteLineAsync("RTSP/1.0 200 OK").ConfigureAwait(false);
                    await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                    if (sessionId is not null)
                    {
                        await writer.WriteLineAsync($"Session: {sessionId}").ConfigureAwait(false);
                    }
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    break;
                case "TEARDOWN":
                    await writer.WriteLineAsync("RTSP/1.0 200 OK").ConfigureAwait(false);
                    await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    return;
                default:
                    await writer.WriteLineAsync("RTSP/1.0 405 Method Not Allowed").ConfigureAwait(false);
                    await writer.WriteLineAsync($"CSeq: {cseq}").ConfigureAwait(false);
                    await writer.WriteLineAsync().ConfigureAwait(false);
                    break;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            _acceptTask?.Wait();
        }
        catch
        {
            // ignore
        }
        _cts.Dispose();
    }
}
