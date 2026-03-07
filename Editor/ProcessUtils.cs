#pragma warning disable IDE0130
using System;
using System.IO;
using System.Diagnostics;

namespace Neovim.Editor
{
  public static class ProcessUtils
  {
#if UNITY_EDITOR_WIN
    /// <summary>
    /// Tries to get the window handle from the windowed process (only on Windows platforms).
    /// </summary>
    /// <param name="p">windowed process from which the window handle is fetched.</param>
    /// <param name="processStartupTimeout">timeout that will be passed to WaitForInputIdle() for the process to finish
    /// starting.</param>
    /// <returns>window handle (guaranteed not to be IntPtr.Zero).</returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="TimeoutException">process timed out</exception>
    public static IntPtr GetWindowHandle(Process p, int processStartupTimeout)
    {
      // make sure to wait until the process finishes starting (remember that Start())
      // does NOT block until the process has actually started)
      if (!p.WaitForInputIdle(processStartupTimeout))
        throw new TimeoutException();
      // refresh/update the process' properties (we only care about the window handle)
      p.Refresh();
      IntPtr wh = p.MainWindowHandle;
      if (wh == IntPtr.Zero)
        throw new InvalidOperationException();
      return wh;
    }
#endif

    /// <summary>
    /// Runs `which` cmd on Linux or `where.exe` on Windows and attemps to extract the full path.
    /// </summary>
    /// <param name="cmd"></param>
    /// <returns>either the full path of the cmd, or null if not found.</returns>
    /// <exception cref="ExitCodeMismatchException">non-zero exit code returned.</exception>
    /// <exception cref="TimeoutException">process timed out</exception>
    public static string CmdPath(string cmd, int timeout)
    {
      using Process p = HeadlessProcess();
#if UNITY_EDITOR_WIN
      // the 'which' cmd equivalent in Windows is 'where.exe'
      p.StartInfo.FileName = "where.exe";
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      p.StartInfo.FileName = "which";
#endif
      p.StartInfo.Arguments = cmd;
      try
      {
        p.RunWithAssertion(timeout);
      }
      catch (ExitCodeMismatchException e) when (e.Actual == 1)
      {
        return null;
      }
      var path = p.StandardOutput.ReadLine();
      if (!File.Exists(path))
        return null;
      return path;
    }

    /// <summary>
    /// Creates a headless (windowless) process with redirected stdout, stderr, and stdin.
    /// </summary>
    /// <returns>The created headless process.</returns>
    public static Process HeadlessProcess()
    {
      var p = new Process();
      p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      p.StartInfo.CreateNoWindow = true;
      p.StartInfo.UseShellExecute = false;
      p.StartInfo.RedirectStandardOutput = true;
      p.StartInfo.RedirectStandardError = true;
      p.StartInfo.RedirectStandardInput = true;
      return p;
    }

    /// <summary>
    /// Runs the provided process and insures that the spawned process is killed even if it fails to exit within the
    /// provided <paramref name="timeout"/>.
    /// </summary>
    /// <param name="Process"></param>
    /// <param name="timeout">timeout to wait for the process in milliseconds. If failed, the process is killed and
    /// TimeoutException is thrown.</param>
    /// <param name="expected">expected exit code. Defaults to 0.</param>
    /// <exception cref="ExitCodeMismatchException">non-zero exit code returned.</exception>
    /// <exception cref="TimeoutException">process timed out</exception>
    public static void RunWithAssertion(this Process p, int timeout, int expected = 0)
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
        throw new TimeoutException($"[neovim.ide] process `{p.StartInfo.FileName}` with args "
            + $"`{p.StartInfo.Arguments}` timed out after {timeout} milliseconds");
      }
      if (p.ExitCode != expected)
      {
        throw new ExitCodeMismatchException($"`{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}`", expected, p.ExitCode);
      }
    }
  }

  public class ExitCodeMismatchException : Exception
  {
    public readonly int Expected;
    public readonly int Actual;
    public ExitCodeMismatchException(string process, int expected, int actual) : base($"[neovim.ide] process {process} didn't match in exit code, expected {expected}, got {actual}")
    {
      Expected = expected;
      Actual = actual;
    }
  }
}
