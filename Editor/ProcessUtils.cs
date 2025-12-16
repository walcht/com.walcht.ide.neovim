#pragma warning disable IDE0130
using System;
using System.Diagnostics;
using System.Collections.Generic;

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


    /// <summary>
    ///   This is expected to be mainly used to spawn processes that are expected to exit
    ///   almost immediately (e.g., think of spawning a process to send a Neovim request).
    ///   This is also used to avoid cross-platform issues where, for instance, on Windows
    ///   a call to WaitForExit(timeout) may not behave in an expected way.
    /// </summary>
    public static void RunProcessAndKillAfter(string app, string args, int timeout = 500)
    {
      using Process p = new();
      p.StartInfo.FileName = app;
      p.StartInfo.Arguments = args;
      p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      p.StartInfo.CreateNoWindow = true;
      p.StartInfo.UseShellExecute = false;

      p.Start();

      if (!p.WaitForExit(timeout))
      {
        p.Kill();
      }
    }


    public static bool RunShellCmd(string cmd, int timeout = 200)
    {
      bool success = false;
      try
      {
        using Process p = new();
#if UNITY_EDITOR_LINUX
          string escapedArgs = escapedArgs = cmd.Replace("\"", "\\\"");
          p.StartInfo.FileName = Environment.GetEnvironmentVariable("SHELL");
          p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
#else // UNITY_EDITOR_WIN
        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.Arguments = $"/C \"{cmd}\"";
#endif
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.UseShellExecute = false;

        p.Start();

        if (p.WaitForExit(timeout))
        {
          success = p.ExitCode == 0;
        }
        else
        {
          p.Kill();
        }
      }
      catch (Exception) { }
      return success;
    }

    /// runs `which` cmd on Linux or `where.exe` on Windows.
    public static bool CheckCmdExistence(string cmd, int timeout = 200)
    {
      bool success = false;
      try
      {
        using Process p = new();
#if UNITY_EDITOR_LINUX
          p.StartInfo.FileName = "which";
#else  // UNITY_EDITOR_WIN
        // the 'which' cmd equivalent in Windows is 'where.exe'
        p.StartInfo.FileName = "where.exe";
#endif
        p.StartInfo.Arguments = cmd;
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.UseShellExecute = false;
        p.Start();
        if (p.WaitForExit(timeout))
        {
          // which/where.exe returns 0 if the supplied cmd was found
          success = p.ExitCode == 0;
        }
        else
        {
          p.Kill();
        }
      }
      catch (Exception) { }
      return success;
    }


    /// runs cmd and gets its standard output
    public static bool GetCmdStdOutput(string cmd, out List<string> lines, int max_nbr_lines = 1, int timeout = 500)
    {
      bool success = false;
      lines = new(max_nbr_lines);
      int lines_read = 0;
      try
      {
        using Process p = new();
#if UNITY_EDITOR_LINUX
        string escapedArgs = escapedArgs = cmd.Replace("\"", "\\\"");
        p.StartInfo.FileName = Environment.GetEnvironmentVariable("SHELL");
        p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
#else // UNITY_EDITOR_WIN
        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.Arguments = $"/C {cmd}";
#endif
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();


        if (p.WaitForExit(timeout))
        {
          string line = null;
          while (lines_read < max_nbr_lines &&
              ((line = p.StandardOutput.ReadLine()) != null) &&
              !string.IsNullOrWhiteSpace(line))
          {
            lines.Add(line);
            ++lines_read;
          }
          success = p.ExitCode == 0;
        }
        else
        {
          p.Kill();
        }
      }
      catch (Exception) { }
      return success;
    }
  }
}
