#pragma warning disable IDE0130
using System;
using System.IO;
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

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
    public static bool IsUnixSocketAlive(string path)
    {
      if (!File.Exists(path)) return false;
      try
      {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(path));
        return true;
      }
      catch (SocketException)
      {
        // stale socket — clean it up so the next launch works cleanly
        try { File.Delete(path); } catch (Exception) { }
        return false;
      }
    }
#endif // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX

    public static bool IsPortInUse(string ip, int port)
    {
      IPAddress _ip = IPAddress.Parse(ip);
      try
      {
        TcpListener list = new(_ip, port);
        list.Start();
        list.Stop();
      }
      catch (SocketException)
      {
        return true;
      }
      return false;
    }
  }
}
