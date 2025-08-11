using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MediaMtx.Net.Rtsp.Tests;

public class RtspServerTests
{
    [Fact]
    public async Task HandlesBasicRtspFlow()
    {
        using var server = new RtspServer(new IPEndPoint(IPAddress.Loopback, 0));
        server.Start();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteAsync("OPTIONS rtsp://localhost/stream RTSP/1.0\r\nCSeq: 1\r\n\r\n");
        var response = await ReadResponseAsync(reader);
        response.Should().Contain("200").And.Contain("Public");

        await writer.WriteAsync("DESCRIBE rtsp://localhost/stream RTSP/1.0\r\nCSeq: 2\r\nAccept: application/sdp\r\n\r\n");
        response = await ReadResponseAsync(reader);
        response.Should().Contain("application/sdp");

        await writer.WriteAsync("SETUP rtsp://localhost/stream/trackID=0 RTSP/1.0\r\nCSeq: 3\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n");
        response = await ReadResponseAsync(reader);
        response.Should().Contain("Session:");
        var sessionLine = response.Split('\n').First(l => l.StartsWith("Session"));
        var sessionId = sessionLine.Split(':')[1].Trim();

        await writer.WriteAsync($"PLAY rtsp://localhost/stream RTSP/1.0\r\nCSeq: 4\r\nSession: {sessionId}\r\n\r\n");
        response = await ReadResponseAsync(reader);
        response.Should().Contain("200");

        await writer.WriteAsync($"TEARDOWN rtsp://localhost/stream RTSP/1.0\r\nCSeq: 5\r\nSession: {sessionId}\r\n\r\n");
        response = await ReadResponseAsync(reader);
        response.Should().Contain("200");
    }

    private static async Task<string> ReadResponseAsync(StreamReader reader)
    {
        var sb = new StringBuilder();
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            sb.Append(line);
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
