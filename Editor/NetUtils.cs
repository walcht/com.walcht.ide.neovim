#pragma warning disable IDE0130
using System.Net;
using System.Net.Sockets;

namespace Neovim.Editor
{
  public static class NetUtils
  {
    private static readonly IPEndPoint DefaultLoopbackEp = new(IPAddress.Loopback, port: 0);
    public static int GetRandomAvailablePort()
    {
      using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      socket.Bind(DefaultLoopbackEp);
      return ((IPEndPoint)socket.LocalEndPoint).Port;
    }

    public static bool IsPortInUse(string ip, int port)
    {
      IPAddress _ip = IPAddress.Parse(ip);
      try
      {
        TcpListener list = new(_ip, port);
        list.Start();
      }
      catch (SocketException)
      {
        return true;
      }
      return false;
    }
  }
}
