using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Neovim.Editor
{
  public static class ProcessUtils {
      public static bool RunProcess(string app, string args, ProcessWindowStyle winStyle,
          bool createNoWindow = false, int timeout = 500)
      {
        bool success = false;
        try
        {
          using (Process p = new ())
          {
            p.StartInfo.FileName = app;
            p.StartInfo.Arguments = args;
            p.StartInfo.WindowStyle = winStyle;
            p.StartInfo.CreateNoWindow = createNoWindow;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            if (!p.WaitForExit(timeout))
            {
              // had to do this because some cmds (e.g., alacritty) don't detach
              // the created child process
              success = true;
            } else
            {
              success = (p.ExitCode == 0);
            }
          }
        }
        catch (Exception)
        {
          success = false;
        }
        return success;
      }


#if UNITY_EDITOR_LINUX
      public static bool RunShellCmd(string cmd, int timeout = 500)
      {
        bool success = false;
        string escapedArgs = escapedArgs = cmd.Replace("\"", "\\\"");
        try
        {
          using (Process p = new ())
          {
            p.StartInfo.FileName = Environment.GetEnvironmentVariable("SHELL");
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            if (!p.WaitForExit(timeout))
            {
              success = false;
            } else
            {
              success = (p.ExitCode == 0);
            }
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
          using (Process p = new ())
          {
            p.StartInfo.FileName = "which";
            p.StartInfo.Arguments = cmd;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            if (!p.WaitForExit(timeout))
            {
              success = false;
            } else
            {
              // which returns 0 if the supplied cmd was found
              success = (p.ExitCode == 0);
            }
          }
        }
        catch (Exception)
        {
          success = false;
        }
        return success;
      }
#endif
  }
}
