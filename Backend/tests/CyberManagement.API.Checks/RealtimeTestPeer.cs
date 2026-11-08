using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CyberManagement.API.Realtime;
using Microsoft.AspNetCore.TestHost;

// Minimal JSON framing for integration checks; production clients use the official SignalR client.
internal sealed class RealtimeTestPeer(WebSocket socket) : IAsyncDisposable
{
    private readonly Queue<JsonElement> _messages = new();
    private string _pending = "";

    public static async Task<RealtimeTestPeer> ConnectAsync(TestServer server, string token, bool queryToken = false)
    {
        var client = server.CreateWebSocketClient();
        if (!queryToken)
            client.ConfigureRequest = request => request.Headers["Authorization"] = $"Bearer {token}";
        var suffix = queryToken ? $"?access_token={Uri.EscapeDataString(token)}" : "";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var socket = await client.ConnectAsync(new Uri($"ws://localhost{RealtimeHub.Endpoint}{suffix}"), timeout.Token);
        var peer = new RealtimeTestPeer(socket);
        try
        {
            await peer.SendAsync(new { protocol = "json", version = 1 });
            var handshake = await peer.NextAsync();
            if (handshake.EnumerateObject().Any())
                throw new InvalidOperationException("SignalR handshake was rejected.");
            var ready = await peer.NextAsync();
            if (ready.GetProperty("target").GetString() != "RealtimeReady")
                throw new InvalidOperationException("Connection did not finish joining its server-assigned groups.");
            return peer;
        }
        catch
        {
            await peer.DisposeAsync();
            throw;
        }
    }

    public async Task SendAsync(object message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + '\u001e');
        await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }

    public async Task<JsonElement> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            if (_messages.TryDequeue(out var message))
            {
                if (message.TryGetProperty("type", out var type) && type.GetInt32() == 6)
                    continue;
                return message;
            }

            using var payload = new MemoryStream();
            var buffer = new byte[4096];
            ValueWebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                    return JsonSerializer.SerializeToElement(new { type = 7 });
                payload.Write(buffer, 0, received.Count);
                if (payload.Length > 65536)
                    throw new InvalidOperationException("Unexpectedly large SignalR test message.");
            } while (!received.EndOfMessage);

            _pending += Encoding.UTF8.GetString(payload.ToArray());
            int separator;
            while ((separator = _pending.IndexOf('\u001e')) >= 0)
            {
                var json = _pending[..separator];
                _pending = _pending[(separator + 1)..];
                using var document = JsonDocument.Parse(json);
                _messages.Enqueue(document.RootElement.Clone());
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        socket.Abort();
        socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
