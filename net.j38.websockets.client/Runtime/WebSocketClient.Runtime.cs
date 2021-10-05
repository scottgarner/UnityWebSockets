using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.WebSockets;
using System.Collections.Generic;

using Debug = UnityEngine.Debug;

namespace J38.WebSockets.Client
{
    public class WebSocketClient
    {
        public delegate void ConnectAction();
        public event ConnectAction OnConnected;

        public delegate void ReceiveAction(string message);
        public event ReceiveAction OnReceived;

        private ClientWebSocket webSocket;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        public WebSocketClient(string webSocketURL)
        {
            if (!String.IsNullOrEmpty(webSocketURL))
            {
                Connect(webSocketURL);
            }
        }

        ~WebSocketClient()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
        }

        public async void Connect(string url)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                using (var timeoutTokenSource = new CancellationTokenSource(5000))
                {
                    using (webSocket = new ClientWebSocket())
                    {
                        webSocket.Options.SetRequestHeader("User-Agent", "Unity3D");

                        try
                        {
                            Debug.Log("<color=cyan>WebSocket connecting.</color>");
                            await webSocket.ConnectAsync(new Uri(url), timeoutTokenSource.Token);

                            if (OnConnected != null) OnConnected();

                            Debug.Log("<color=cyan>WebSocket receiving.</color>");
                            await Receive();

                            Debug.Log("<color=cyan>WebSocket closed.</color>");

                        }
                        catch (OperationCanceledException)
                        {
                            Debug.Log("<color=cyan>WebSocket shutting down.</color>");
                        }
                        catch (WebSocketException)
                        {
                            Debug.Log("<color=cyan>WebSocket connection lost.</color>");
                            //Debug.LogWarning(e);
                        }
                        catch (Exception ex)
                        {
                            Debug.Log(ex);
                            throw;
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Debug.Log("<color=cyan>Websocket reconnecting.</color>");
                        await Task.Delay(5000);
                    }

                }
            }

            Debug.Log("<color=cyan>Websocket shutting down.</color>");
        }

        public void Send(string message)
        {
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                var encoded = Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);

                webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
            }
        }

        private async Task Receive()
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = null;
                var allBytes = new List<byte>();

                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    for (int i = 0; i < result.Count; i++)
                    {
                        allBytes.Add(buffer.Array[i]);
                    }
                }
                while (!result.EndOfMessage);

                var message = Encoding.UTF8.GetString(allBytes.ToArray(), 0, allBytes.Count);
                if (OnReceived != null) OnReceived(message);
            }
        }
    }
}