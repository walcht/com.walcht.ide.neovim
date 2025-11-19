using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Neovim.Editor
{
  public static class ProcessUtils
  {
    public static bool RunProcess(string app, string args, ProcessWindowStyle winStyle, bool createNoWindow = false,
    bool useShellExecute = false)
    {
      try
      {
        using (Process p = new())
        {
          p.StartInfo.FileName = app;
          p.StartInfo.Arguments = args;
          p.StartInfo.WindowStyle = winStyle;
          p.StartInfo.CreateNoWindow = false;
          p.StartInfo.UseShellExecute = useShellExecute;

          p.Start();
          return true;
        }
      }
      catch (Exception)
      {
        return false;
      }
    }


    public static void RunProcessAndExitImmediately(string app, string args)
    {
      Debug.Log(app + " " + args);
      try
      {
        using (Process p = new())
        {
          p.StartInfo.FileName = app;
          p.StartInfo.Arguments = args;
          p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
          p.StartInfo.CreateNoWindow = true;
          p.StartInfo.UseShellExecute = false;

          p.Start();

          // WaitForExitAsync is only available in newer .NET versions
          p.WaitForExit(50);

          p.Kill();
        }
      }
      catch (Exception e) { }
    }


    public static bool RunShellCmd(string cmd, int timeout = 500)
    {
      bool success = false;
        Debug.Log(cmd);
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
          p.WaitForExit();
          success = p.ExitCode == 0;
        }
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }

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
          if (!p.WaitForExit(timeout))
          {
            success = false;
          }
          else
          {
            // which/where.exe returns 0 if the supplied cmd was found
            success = p.ExitCode == 0;
          }
        }
      }
      catch (Exception)
      {
        success = false;
      }
      return success;
    }
  }
}
