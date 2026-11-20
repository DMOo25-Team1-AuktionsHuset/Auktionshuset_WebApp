using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Auktionshuset.Api.Endpoints.Device
{
    public static class DeviceConnectionTestEndpoint
    {
        public static IEndpointRouteBuilder MapDeviceConnectionTest(
            this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/ws/device/test", HandleAsync)
                .AllowAnonymous();

            return endpoints;
        }

        private static async Task HandleAsync(HttpContext context)
        {
            if(!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await context.Response.WriteAsync(
                    "Dette endpoint kraever websocket forbindelse.");

                return;

            }

            using WebSocket socket =
                await context.WebSockets.AcceptWebSocketAsync();

            CancellationToken cancellationToken =
                context.RequestAborted;

            // max 1 KB på Testprotokol
            byte[] buffer = new byte[1024];
            int receivedBytes = 0;

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(
                        buffer.AsMemory(receivedBytes),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Forbindelsen lukkes.",
                            cancellationToken);

                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.InvalidMessageType,
                        "Kun tekstbeskeder understøttes.",
                        cancellationToken);

                    return;
                }

                receivedBytes += result.Count;

                    if (!result.EndOfMessage)
                    {
                        if (receivedBytes == buffer.Length)
                        {
                            await socket.CloseAsync(
                                WebSocketCloseStatus.MessageTooBig,
                                "Beskeden er for stor.",
                                cancellationToken);

                            return;
                        }

                        continue;
                   }

                    string response;

                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(
                            buffer.AsMemory(0, receivedBytes));

                        JsonElement root = document.RootElement;

                        bool isHello =
                            root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("type", out var type) &&
                            type.ValueKind == JsonValueKind.String &&
                            type.GetString() == "hello";

                        response = isHello
                             ? """
                            {"type":"helloAck","message":"Hello from auktionshuset"}
                            """
                             : """
                            {"type":"error","message":"Forventede type hello"}
                            """;
                    }

                    catch (JsonException)
                    {
                        response = """
                            {"type":"error","message":"Ugyldigt JSON"}
                            """;
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                    await socket.SendAsync(
                        responseBytes.AsMemory(),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: cancellationToken);

                    receivedBytes = 0;
                        
                }
            }

            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Forbindelsen eller serveren blev stoppet.
            }

            catch (WebSocketException)
            {
                // Klienten mistede forbindelsen uden en normal lukning.
            }
        }
    }
}
