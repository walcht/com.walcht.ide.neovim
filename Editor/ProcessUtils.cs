#pragma warning disable IDE0130
using System;
using System.IO;
using System.Diagnostics;

namespace Neovim.Editor
{
  public static class ProcessUtils
  {
#if UNITY_EDITOR_WIN
    public static IntPtr GetWindowHandle(Process p, int processStartupTimeout)
    {
      // make sure to wait until the process finishes starting (remember that Start())
      // does NOT block until the process has actually started)
      if (!p.WaitForInputIdle(processStartupTimeout))
        throw new InvalidOperationException();

      // refresh/update the process' properties (we only care about the window handle)
      p.Refresh();
      IntPtr wh = p.MainWindowHandle;
      if (wh == IntPtr.Zero)
        throw new InvalidOperationException();

      return wh;

    }
#endif

    /// runs `which` cmd on Linux or `where.exe` on Windows.
    /// returns either the full path of the cmd, or null if not found
    public static string CmdPath(string cmd)
    {
      using Process p = HeadlessProcess();
#if UNITY_EDITOR_LINUX
      p.StartInfo.FileName = "which";
#else  // UNITY_EDITOR_WIN
      // the 'which' cmd equivalent in Windows is 'where.exe'
      p.StartInfo.FileName = "where.exe";
#endif
      p.StartInfo.Arguments = cmd;
      p.RunWithAssertion(200, 0);
      var path = p.StandardOutput.ReadLine();
      if (!File.Exists(path))
        return null;
      return path;
    }

    public static Process HeadlessProcess()
    {
      var p = new Process();
      p.StartInfo.RedirectStandardOutput = true;
      p.StartInfo.RedirectStandardError = true;
      p.StartInfo.RedirectStandardInput = true;
      p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      p.StartInfo.CreateNoWindow = true;
      p.StartInfo.UseShellExecute = false;
      p.StartInfo.RedirectStandardOutput = true;
      return p;
    }

    public static void RunWithAssertion(this Process p, int timeout, int expected)
    {
      p.Start();
      if (!p.WaitForExit(timeout))
      {
        try
        {
          p.Kill();
        }
        catch (Exception e)
        {
          UnityEngine.Debug.LogError($"[neovim.ide] failed to kill process before timeout assertion error. Raison: {e.Message}");
        }
        throw new TimeoutException($"Process `{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}` timed out after {timeout} milliseconds");
      }
      if (p.ExitCode != expected)
      {
        throw new ExitCodeMismatchException($"Process `{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}` didn't match in exit code, expected {expected}, got {p.ExitCode}");
      }
    }
  }

  public class ExitCodeMismatchException : Exception
  {
    public ExitCodeMismatchException(string message) : base(message) { }
  }
}
