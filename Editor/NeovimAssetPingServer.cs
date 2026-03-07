#pragma warning disable IDE0130
using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Neovim.Editor
{
  [InitializeOnLoad]
  public static class NeovimAssetPingServer
  {
    private const int DefaultPort = 54321;

    // Written on start, deleted on stop — nvim reads this to find the right port
    // when multiple Unity editors are running simultaneously.
    private static readonly string s_PortFilePath = Path.GetFullPath(
      Path.Combine(Application.dataPath, "..", "Library", ".unity-ping-port"));

    private static HttpListener s_Listener;
    private static int s_CurrentPort = -1;
    private static string s_PendingPingPath = null;
    private static string s_PendingSelectPath = null;
    private static readonly object s_Lock = new();

    public static bool IsRunning => s_Listener != null && s_Listener.IsListening;
    public static int CurrentPort => s_CurrentPort;

    static NeovimAssetPingServer()
    {
      // AssetImportWorker and other batch-mode processes also run [InitializeOnLoad]
      // but have no editor UI — skip them so they don't overwrite the port file.
      if (Application.isBatchMode) return;

      Start();
      EditorApplication.quitting += Stop;
      AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }

    public static void Restart()
    {
      Stop();
      Start();
    }

    private static void Start()
    {
      int port = BindListener();
      if (port < 0) return;

      s_CurrentPort = port;
      try
      {
        File.WriteAllText(s_PortFilePath, port.ToString());
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] failed to write port file: {e.Message}");
      }

      BeginAccept();
      EditorApplication.update += ProcessPendingPing;
    }

    // Try DefaultPort first; if taken, fall back to any available port.
    // Returns the bound port, or -1 on failure.
    private static int BindListener()
    {
      s_Listener = new HttpListener();
      s_Listener.Prefixes.Add($"http://localhost:{DefaultPort}/");
      try
      {
        s_Listener.Start();
        return DefaultPort;
      }
      catch (Exception) { }

      // DefaultPort busy — pick a free port and retry
      int fallback = NetUtils.GetRandomAvailablePort();
      s_Listener = new HttpListener();
      s_Listener.Prefixes.Add($"http://localhost:{fallback}/");
      try
      {
        s_Listener.Start();
        return fallback;
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] asset ping server failed to start: {e.Message}");
        return -1;
      }
    }

    private static void Stop()
    {
      EditorApplication.update -= ProcessPendingPing;
      try { s_Listener?.Stop(); } catch (Exception) { }
      try { s_Listener?.Close(); } catch (Exception) { }
      try { File.Delete(s_PortFilePath); } catch (Exception) { }
      s_CurrentPort = -1;
    }

    private static void BeginAccept()
    {
      try { s_Listener.BeginGetContext(OnContext, null); }
      catch (Exception) { }
    }

    // called on threadpool — only store path, never touch Unity API here
    private static void OnContext(IAsyncResult ar)
    {
      try
      {
        var ctx = s_Listener.EndGetContext(ar);
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        string path = reader.ReadToEnd().Trim();

        ctx.Response.StatusCode = 200;
        ctx.Response.Close();

        if (!string.IsNullOrEmpty(path))
        {
          lock (s_Lock)
          {
            if (ctx.Request.Url.AbsolutePath == "/select-asset")
              s_PendingSelectPath = path;
            else
              s_PendingPingPath = path;
          }
        }
      }
      catch (Exception) { }
      finally { BeginAccept(); }
    }

    // called on Unity main thread via EditorApplication.update
    private static void ProcessPendingPing()
    {
      string pingPath, selectPath;
      lock (s_Lock)
      {
        pingPath = s_PendingPingPath;
        s_PendingPingPath = null;
        selectPath = s_PendingSelectPath;
        s_PendingSelectPath = null;
      }

      if (pingPath != null)
        PingAsset(pingPath);

      if (selectPath != null)
        SelectAsset(selectPath);
    }

    private static void PingAsset(string path)
    {
      string relativePath = FileUtil.GetProjectRelativePath(path);
      if (string.IsNullOrEmpty(relativePath)) return;
      var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
      if (asset == null) return;
      EditorGUIUtility.PingObject(asset);
    }

    private static void SelectAsset(string path)
    {
      string relativePath = FileUtil.GetProjectRelativePath(path);
      if (string.IsNullOrEmpty(relativePath)) return;
      var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
      if (asset == null) return;
      Selection.activeObject = asset;
      EditorGUIUtility.PingObject(asset);
    }
  }
}
