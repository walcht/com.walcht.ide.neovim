#pragma warning disable IDE0130
using UnityEditor;
using UnityEngine;
using SysDiag = System.Diagnostics;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimReset : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Reset Config")]
    static void ResetConfig()
    {
      EditorPrefs.DeleteKey("NvimUnityConfigJson");
      NeovimCodeEditor.InitConfig();
      Debug.Log("[neovim.ide] reset the previously saved neovim config");
    }

    /// <summary>
    /// Kill all orphaned nvim server processes that may be left behind after Unity crashes.
    /// Handles both legacy (/tmp/nvimsocket) and per-instance (/tmp/nvimsocket_<PID>) socket patterns.
    /// This resolves issues where the plugin hangs because the socket is held by a zombie process.
    /// </summary>
    [MenuItem("Neovim/Kill Orphaned Server")]
    static void KillServer()
    {
      int killedCount = 0;

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      // Find nvim processes listening on Unity nvim sockets (both old and new patterns)
      var psi = new SysDiag.ProcessStartInfo
      {
        FileName = "/bin/sh",
        Arguments = "-c \"ps aux | grep 'nvim.*--listen.*nvimsocket' | grep -v grep | awk '{print $2}'\"",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var p = SysDiag.Process.Start(psi);
      if (p != null)
      {
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        var pids = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string pidStr in pids)
        {
          if (int.TryParse(pidStr, out int pid))
          {
            try
            {
              // Try SIGTERM first
              var killPsi = new SysDiag.ProcessStartInfo
              {
                FileName = "/bin/sh",
                Arguments = $"-c \"kill {pid} 2>/dev/null\"",
                UseShellExecute = false,
                CreateNoWindow = true
              };
              using var killP = SysDiag.Process.Start(killPsi);
              killP?.WaitForExit();

              // Give it a moment, then force kill if still alive
              System.Threading.Thread.Sleep(100);
              killPsi.Arguments = $"-c \"kill -9 {pid} 2>/dev/null\"";
              using var killP2 = SysDiag.Process.Start(killPsi);
              killP2?.WaitForExit();

              killedCount++;
            }
            catch (System.Exception e)
            {
              Debug.LogWarning($"[neovim.ide] failed to kill nvim process {pid}: {e.Message}");
            }
          }
        }
      }

      // Also clean up all Unity nvim socket files (both old and new patterns)
      var cleanPsi = new SysDiag.ProcessStartInfo
      {
        FileName = "/bin/sh",
        Arguments = "-c \"rm -f /tmp/nvimsocket /tmp/nvimsocket_*\"",
        UseShellExecute = false,
        CreateNoWindow = true
      };
      using var cleanP = SysDiag.Process.Start(cleanPsi);
      cleanP?.WaitForExit();

#elif UNITY_EDITOR_WIN
      // Windows: find nvim processes with --listen argument
      var psi = new SysDiag.ProcessStartInfo
      {
        FileName = "powershell",
        Arguments = "-Command \"Get-Process nvim -ErrorAction SilentlyContinue | Where-Object {$_.Path -like '*--listen*'} | Select-Object -ExpandProperty Id\"",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var p = SysDiag.Process.Start(psi);
      if (p != null)
      {
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        var pids = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string pidStr in pids)
        {
          if (int.TryParse(pidStr, out int pid))
          {
            try
            {
              var proc = SysDiag.Process.GetProcessById(pid);
              proc.Kill();
              killedCount++;
            }
            catch (System.Exception e)
            {
              Debug.LogWarning($"[neovim.ide] failed to kill nvim process {pid}: {e.Message}");
            }
          }
        }
      }
#endif

      if (killedCount > 0)
      {
        Debug.Log($"[neovim.ide] killed {killedCount} orphaned nvim server process(es). You can now open files in Unity.");
      }
      else
      {
        Debug.Log("[neovim.ide] no orphaned nvim server processes found. The plugin should work normally.");
      }
    }

    /// <summary>
    /// Combined reset: kill server AND reset config. Use this when Unity crashes leave the plugin in a bad state.
    /// </summary>
    [MenuItem("Neovim/Force Reset (Kill Server + Reset Config)")]
    static void ForceReset()
    {
      KillServer();
      ResetConfig();
      Debug.Log("[neovim.ide] force reset complete. Try opening a file now.");
    }

    /// <summary>
    /// Toggle whether nvim server is killed when Unity quits.
    /// When OFF (default): nvim stays running after Unity closes - preserves your session.
    /// When ON: nvim is killed on quit - prevents orphaned processes.
    /// </summary>
    [MenuItem("Neovim/Toggle Kill Nvim on Quit")]
    static void ToggleKillOnQuit()
    {
      var config = NeovimCodeEditor.s_Config;
      config.KillNvimOnQuit = !config.KillNvimOnQuit;
      config.Save();

      string status = config.KillNvimOnQuit ? "ON" : "OFF";
      Debug.Log($"[neovim.ide] Kill Nvim on Quit: {status}");
      Debug.Log($"[neovim.ide] When OFF, nvim will stay running after Unity closes (preserves session).");
      Debug.Log($"[neovim.ide] When ON, nvim will be killed when Unity quits (prevents orphans).");
    }

    /// <summary>
    /// Validate the toggle menu item to show a checkmark when enabled.
    /// Always returns true so the menu is always enabled.
    /// </summary>
    [MenuItem("Neovim/Toggle Kill Nvim on Quit", true)]
    static bool ValidateToggleKillOnQuit()
    {
      Menu.SetChecked("Neovim/Toggle Kill Nvim on Quit", NeovimCodeEditor.s_Config.KillNvimOnQuit);
      return true; // Always enable the menu item
    }
  }
}
