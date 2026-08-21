using System;
using UnityEngine;

#if UNITY_EDITOR_WIN
using System.IO;
using System.Runtime.InteropServices;
#endif

namespace Neovim.Editor
{
  public interface INeovimWindowFocus
  {
    bool IsAvailable { get; }

    void Focus();
  }

  public class FallbackNeovimWindowFocus : INeovimWindowFocus
  {
    public bool IsAvailable => false;

    public void Focus() { }
  }

#if UNITY_EDITOR_LINUX
  public enum LinuxDesktopEnvironment
  {
    X11, // if we are on X11 - wmctrl solves our window focusing issues
    GNOME, // GNOME (e.g., Ubuntu) on Wayland
    KDE, // KDE on Wayland
    Hyprland,
    OTHER,
    UNKNOWN, // can't be determined :/
  }

  public class LinuxNeovimWindowFocus : INeovimWindowFocus
  {
    private readonly LinuxDesktopEnvironment m_CurrentEnvironment;
    private readonly INeovimWindowFocus m_ConcreteFocus;
    private readonly NeovimEditorConfig m_Conig;

    public bool IsAvailable => m_ConcreteFocus.IsAvailable;

    public LinuxNeovimWindowFocus(NeovimEditorConfig config)
    {
      m_Conig = config;
      m_CurrentEnvironment = DetermineLinuxDesktopEnvironment();

      switch (m_CurrentEnvironment)
      {
        case LinuxDesktopEnvironment.X11:
          m_ConcreteFocus = new X11NeovimWindowFocus(config);
          break;
        case LinuxDesktopEnvironment.GNOME:
          m_ConcreteFocus = new GnomeNeovimWindowFocus(config);
          break;
        case LinuxDesktopEnvironment.Hyprland:
          m_ConcreteFocus = new HyprlandNeovimWindowFocus(config);
          break;
        case LinuxDesktopEnvironment.KDE: // TODO: KDE focus implementation
        default:
          m_ConcreteFocus = new FallbackNeovimWindowFocus();
          break;
      }
    }

    public void Focus()
    {
      if (IsAvailable)
        m_ConcreteFocus.Focus();
    }

    private LinuxDesktopEnvironment DetermineLinuxDesktopEnvironment()
    {
      string session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"); // session can be x11, wayland or tty

      if (session == "x11")
        return LinuxDesktopEnvironment.X11;

      if (session != "wayland")
        return LinuxDesktopEnvironment.UNKNOWN;

      string currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");

      if (string.IsNullOrEmpty(currentDesktop))
        return LinuxDesktopEnvironment.UNKNOWN;

      //XDG_CURRENT_DESKTOP may have several values, so we check all of them
      foreach (var de in currentDesktop.Split(':'))
      {
        if (de.Equals("gnome", StringComparison.OrdinalIgnoreCase))
          return LinuxDesktopEnvironment.GNOME;

        if (de.Equals("kde", StringComparison.OrdinalIgnoreCase))
          return LinuxDesktopEnvironment.KDE;

        if (de.Equals("hyprland", StringComparison.OrdinalIgnoreCase))
          return LinuxDesktopEnvironment.Hyprland;
      }

      return LinuxDesktopEnvironment.OTHER;
    }
  }

  // TODO: check if wmctrl is available on the system
  public class X11NeovimWindowFocus : INeovimWindowFocus
  {
    private readonly NeovimEditorConfig m_Config;

    public bool IsAvailable { get; private set; }

    public X11NeovimWindowFocus(NeovimEditorConfig config)
    {
      m_Config = config;

      CheckAvailability();
    }

    public void Focus()
    {
      if(!IsAvailable)
        return;

      using var p = ProcessUtils.HeadlessProcess();
      p.StartInfo.FileName = "wmctrl";
      p.StartInfo.Arguments = "-a nvimunity";
      var error_msg =
        "[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity'.\n"
        + $"Reason: cmd `{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}` failed.\n";
      try
      {
        p.RunWithAssertion(m_Config.ProcessTimeout);
      }
      catch (ExitCodeMismatchException)
      {
        Debug.LogWarning($"{error_msg}Reason: non-zero exit code.");
      }
      catch (TimeoutException)
      {
        Debug.LogWarning(
          $"{error_msg}Exception message: timed out after {m_Config.ProcessTimeout} milliseconds."
        );
      }
    }

    private void CheckAvailability()
    {
      if (ProcessUtils.CmdPath("wmctrl", m_Config.ProcessTimeout) == null)
      {
        Debug.LogWarning(
          "[neovim.ide] neovim window focusing feature is not available \n"
            + "Reason: cmd 'wmctrl' is not available. Please install 'wmctrl' for window focusing capability."
        );

        IsAvailable = false;
      }
      else
      {
        IsAvailable = true;
      }
    }
  }

  public class GnomeNeovimWindowFocus : INeovimWindowFocus
  {
    private readonly NeovimEditorConfig m_Config;

    public bool IsAvailable { get; private set; }

    public GnomeNeovimWindowFocus(NeovimEditorConfig config)
    {
      m_Config = config;

      CheckAvailability();
    }

    public void Focus()
    {
      if (!IsAvailable)
        return;

      // a clusterfuck of a mess - TODO: learn gdbus and clean this shit up somehow
      using var p = ProcessUtils.HeadlessProcess();
      p.StartInfo.FileName = "gdbus";
      p.StartInfo.Arguments =
        @"call --session --dest org.gnome.Shell \
--object-path /de/lucaswerkmeister/ActivateWindowByTitle \
--method de.lucaswerkmeister.ActivateWindowByTitle.activateBySubstring 'nvimunity'";
      const string error_msg =
        "[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity'.\n"
        + "Did you logout and login of your GNOME session?\n"
        + "Did you install the 'activate-window-by-title@lucaswerkmeister.de' GNOME extension?\n";
      try
      {
        p.RunWithAssertion(m_Config.ProcessTimeout);
      }
      catch (ExitCodeMismatchException)
      {
        Debug.LogWarning($"{error_msg}Reason: non-zero exit code.");
      }
      catch (TimeoutException)
      {
        Debug.LogWarning(
          $"{error_msg}Exception message: timed out after {m_Config.ProcessTimeout} milliseconds."
        );
      }
    }

    private void CheckAvailability()
    {
      // this prompts the user to install a GNOME extension to focus on a window by title
      // there is unfortunately no other way to do this on GNOME under Wayland :/
      using var p = ProcessUtils.HeadlessProcess();
      p.StartInfo.FileName = "gnome-extensions";
      p.StartInfo.Arguments = "list";
      p.RunWithAssertion(10_000);
      const string uuid = "activate-window-by-title@lucaswerkmeister.de";
      var foundExtension = false;
      string line;
      while ((line = p.StandardOutput.ReadLine()) != null)
      {
        if (line.Contains(uuid))
        {
          foundExtension = true;
          IsAvailable = true;
          break;
        }
      }

      // if the extension is not found, prompt the user to install it
      if (!foundExtension)
      {
        using var p2 = ProcessUtils.HeadlessProcess();
        p2.StartInfo.FileName = "busctl";
        p2.StartInfo.Arguments =
          $"--user call org.gnome.Shell.Extensions /org/gnome/Shell/Extensions org.gnome.Shell.Extensions InstallRemoteExtension s {uuid}";
        p2.Start();
        const string error =
          "[neovim.ide] neovim window focusing feature is not available\n"
          + "Reason: failed to install GNOME extension: activate-window-by-title@lucaswerkmeister.de\n";
        if (!p2.WaitForExit(15_000))
        {
          Debug.LogWarning($"{error}Reason: timed out after 10 seconds");
        }
        else if (p2.ExitCode != 0)
        {
          Debug.LogWarning($"{error}Reason: non-zero exit code ({p2.ExitCode})");
        }
        else
        {
          IsAvailable = true;
        }
      }
    }
  }

  public class HyprlandNeovimWindowFocus : INeovimWindowFocus
  {
    private readonly NeovimEditorConfig m_Config;

    public bool IsAvailable => true;

    public HyprlandNeovimWindowFocus(NeovimEditorConfig config)
    {
      m_Config = config;
    }

    public void Focus()
    {
      using var p = ProcessUtils.HeadlessProcess();
      p.StartInfo.FileName = "hyprctl";
      p.StartInfo.Arguments =
        "eval 'hl.dispatch(hl.dsp.focus({ window = \"initialtitle:^(nvimunity.*)$\"}))'";

      var error_msg =
        "[neovim.ide] failed to focus on Neovim server instance'.\n"
        + $"Reason: cmd `{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}` failed.\n";

      try
      {
        p.RunWithAssertion(m_Config.ProcessTimeout);
      }
      catch (ExitCodeMismatchException)
      {
        Debug.LogWarning($"{error_msg}Reason: non-zero exit code.");
      }
      catch (TimeoutException)
      {
        Debug.LogWarning(
          $"{error_msg}Exception message: timed out after {m_Config.ProcessTimeout} milliseconds."
        );
      }
    }
  }
#endif

#if UNITY_EDITOR_WIN
  public class WindowsNeovimWindowFocus : INeovimWindowFocus
  {
    private readonly string m_ReadWindowHandlePath;
    private readonly NeovimEditorConfig m_Config;

    public bool IsAvailable { get; private set; }

    public WindowsNeovimWindowFocus(NeovimEditorConfig config, string readWindowHandlePath)
    {
      m_Config = config;
      m_ReadWindowHandlePath = readWindowHandlePath;
    }

    public void Focus()
    {
      if (!IsAvailable)
        return;

      IntPtr windowHandle = new IntPtr(Convert.ToInt64(m_Config.PrevServerProcessIntPtrStringRepr));
      ShowWindow(windowHandle, 5); // 5 == Activates the window and displays it in its current size and position
      SetForegroundWindow(windowHandle);
    }

    /// <summary>
    /// Tries to get the window handle of the Neovim server instance process. First it attempts to call GetWindowHandle
    /// directly on the provided process <paramref name="p"> otherwise it executes the GetProcessPPID.ps1 script on
    /// one of its children - which is assumed to have a Window attached to it as is the case with WT - to get its
    /// window handle. If that fails, an error log is shown and window focusing is disabled.
    /// </summary>
    /// <param name="p">Neovim server instance process (i.e., the process that starts Neovim)</param>
    public void TryGetWindowHandle(System.Diagnostics.Process p)
    {
      // the idea here is to figure out the handle of the process running the Neovim server instance
      // this is a bit tricky on Windows - because depending on the terminal launch cmd, it might
      // spawn a child process or it might not.
      //
      // first - we assume that the terminal launch cmd's process is the one that has Neovim server
      // open (i.e., no child process)
      int process_startup_timeout = 1000;
      var errMsg =
        "[neovim.ide] failed to get the PID of Neovim server instance's window. "
        + "Auto window focusing is disabled.";
      try
      {
        IntPtr wh = ProcessUtils.GetWindowHandle(p, process_startup_timeout);
        m_Config.PrevServerProcessIntPtrStringRepr = wh.ToString();
        m_Config.Save();
      }
      // this probably means that the terminal launch cmd spawns a new child instance that is responsible for the Neovim
      // window (e.g., WT).
      catch (InvalidOperationException)
      {
        try
        {
          // Note: on .Net Standard 2.0 (at least on Unity 2019.4) there is a race-condition bug within the
          // NamedPipeClientStream.Connect() instance method. This is the reason why we invoke a Powershell script and
          // just avoid that mess. Read this for details:
          //  https://github.com/dotnet/runtime/pull/65553
          using (var proc = ProcessUtils.HeadlessProcess())
          {
            proc.StartInfo.FileName = "powershell";
            proc.StartInfo.Arguments = $"-File {m_ReadWindowHandlePath}";
            proc.RunWithAssertion(1000);
            var line = proc.StandardOutput.ReadLine();
            if (line != null)
            {
              IntPtr wh = new IntPtr(Convert.ToInt64(line));
              m_Config.PrevServerProcessIntPtrStringRepr = wh.ToString();
              m_Config.Save();
            }
            else
            {
              throw new Exception("PPID received/read string is null");
            }
          }
        }
        catch (Exception e)
        {
          IsAvailable = false;
          Debug.LogWarning(errMsg + $" Reason: {e.Message}");
        }
      }
      catch (Exception)
      {
        IsAvailable = false;
        Debug.LogWarning(errMsg);
      }
    }

    [DllImport("user32.dll")]
    internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  }
#endif
}
