using System.Net;
using System.Net.Sockets;

namespace Neovim.Editor
{
    public static class NetUtils
    {
        private static readonly IPEndPoint DefaultLoopbackEp = new(IPAddress.Loopback, port: 0);
        public static int GetRandomAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(DefaultLoopbackEp);
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}
