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
    private const int Port = 54321;

    private static HttpListener s_Listener;
    private static string s_PendingPingPath = null;
    private static string s_PendingSelectPath = null;
    private static readonly object s_Lock = new();

    static NeovimAssetPingServer()
    {
      s_Listener = new HttpListener();
      s_Listener.Prefixes.Add($"http://localhost:{Port}/");

      try
      {
        s_Listener.Start();
        BeginAccept();
        EditorApplication.update += ProcessPendingPing;
        EditorApplication.quitting += Stop;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] asset ping server failed to start on port {Port}: {e.Message}");
      }
    }

    private static void Stop()
    {
      EditorApplication.update -= ProcessPendingPing;
      try { s_Listener?.Stop(); } catch (Exception) { }
      try { s_Listener?.Close(); } catch (Exception) { }
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
