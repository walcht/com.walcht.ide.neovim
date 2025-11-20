using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Neovim.Editor
{
  public static class ProcessUtils
  {
    /// this is mainly used to avoid cross-platform issues where, for instance, on Windows
    /// a call to WaitForExit(timeout) may not behave in an expected way.
    public static void RunProcessAndKillAfter(string app, string args, int timeout = 200)
    {
      using (Process p = new())
      {
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
    }


    public static bool RunShellCmd(string cmd, int timeout = 200)
    {
      bool success = false;
      try
      {
        using (Process p = new())
        {
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
        using (Process p = new())
        {
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
      }
      catch (Exception) { }
      return success;
    }
  }
}
