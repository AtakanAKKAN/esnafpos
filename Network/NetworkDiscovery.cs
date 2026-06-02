using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EsnafPos.Network
{
    /// <summary>
    /// Aynı ağdaki EsnafPos sunucusunu otomatik keşfeder.
    /// Server: UDP broadcast ile kendini duyurur.
    /// Client: Broadcast ile sunucuyu arar, IP'yi otomatik alır.
    /// </summary>
    public static class NetworkDiscovery
    {
        private const int    DiscoveryPort    = 5151;
        private const string DiscoveryRequest = "ESNAFPOS_DISCOVER";
        private const string ResponsePrefix   = "ESNAFPOS_SERVER:";

        // ─── SERVER TARAFI ────────────────────────────────────
        // App.xaml.cs'te Server modunda çağır:
        //   _ = NetworkDiscovery.StartListenerAsync(net.ServerPort, _discoveryCts.Token);

        public static async Task StartListenerAsync(int apiPort, CancellationToken ct)
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var msg    = Encoding.UTF8.GetString(result.Buffer);

                    if (msg != DiscoveryRequest) continue;

                    // Kendi yerel IP'mi bul
                    var localIp  = GetLocalIp();
                    var response = Encoding.UTF8.GetBytes($"{ResponsePrefix}{localIp}:{apiPort}");
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) { break; }
                catch { /* bağlantı hatası — devam */ }
            }
        }

        // ─── CLIENT TARAFI ────────────────────────────────────
        // Dönen değer: "192.168.1.x:5150" veya null (bulunamadı)

        public static async Task<(string? ip, int port)> DiscoverServerAsync(int timeoutMs = 4000)
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;

                var request   = Encoding.UTF8.GetBytes(DiscoveryRequest);
                var broadcast = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                await udp.SendAsync(request, request.Length, broadcast);

                using var cts    = new CancellationTokenSource(timeoutMs);
                var result       = await udp.ReceiveAsync(cts.Token);
                var response     = Encoding.UTF8.GetString(result.Buffer);

                if (!response.StartsWith(ResponsePrefix)) return (null, 0);

                var address = response[ResponsePrefix.Length..]; // "192.168.1.x:5150"
                var parts   = address.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
                    return (null, 0);

                return (parts[0], port);
            }
            catch
            {
                return (null, 0);
            }
        }

        private static string GetLocalIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 80);
                return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }
    }
}
