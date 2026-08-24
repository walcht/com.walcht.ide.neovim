#pragma warning disable IDE0130, IDE0300, IDE0090, IDE0063, IDE0057
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;


namespace Neovim.Editor
{
  [InitializeOnLoad]
  public class NeovimCodeEditor : IExternalCodeEditor
  {
    private static NeovimEditorConfig s_Config = new NeovimEditorConfig();

    // Unique identifier for this Unity instance (PID)
    public static readonly string s_InstanceId = Process.GetCurrentProcess().Id.ToString();

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
    private static string s_ServerSocket = "/tmp/nvimsocket";
#else // UNITY_EDITOR_WIN
    // this is initialized to some "127.0.0.1:<random-port>" because Unix domain sockets on Windows are a bitch
    // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not
    // available for socket type" so we have to listen to a TCP socket instead with a local addr and a random port
    private static string s_ServerSocket = $"127.0.0.1:{NetUtils.GetRandomAvailablePort()}";
    private static readonly string s_GetProcessWindowHandlePath = Path.GetFullPath("Packages/com.walcht.ide.neovim/GetProcessWindowHandle.ps1");
    private static readonly string s_ReadWindowHandlePath = Path.GetFullPath("Packages/com.walcht.ide.neovim/ReadWindowHandleFromPipeServer.ps1");
#endif
    public static readonly string s_RestartRoslynLSPath = Path.GetFullPath("Packages/com.walcht.ide.neovim/RestartRoslynLS.lua");

    public static NeovimEditorConfig Config
    {
      get => s_Config;
    }

    public static string ServerSocket
    {
      get => s_ServerSocket;
    }

    // add your file extension here if you want it to be opened by Neovim via Unity
    static readonly string[] s_SupportedExtensions = {
      // csharp
      "cs",
      "csproj",
      "sln",
      // python
      "py",
      // shader files
      "shader",
      "cginc",
      // misc
      "txt",
      "json",
      "yaml",
      "xml",
    };

    // Fallback Neovim installation paths (only used in case nvim executable path is not explicitly provided). The first
    // valid path is picked. "nvim(.exe)" is a special case where PATH is checked for its existence.
    private static readonly string[] s_CandidateNeovimPaths =
#if UNITY_EDITOR_LINUX
     {
       "nvim",
       "/usr/bin/nvim",
       "/opt/nvim-linux64/bin/nvim",
       "/opt/nvim-linux-x86_64/bin/nvim",
     };
#elif UNITY_EDITOR_OSX
     {
       "nvim",
       "/usr/local/bin/nvim",
       "/opt/homebrew/bin/nvim",
       "/usr/bin/nvim",
     };
#else // UNITY_EDITOR_WIN
     // make sure to include the extension in the executalbe's name!
     {
       "nvim.exe",  // powershell bitches about missing .exe extension
       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Neovim", "bin", "nvim.exe"),
     };
#endif

    private static IGenerator s_Generator = null;
    private static INeovimWindowFocus s_NeovimFocus = null;

    /// <summary>
    /// Sets the default terminal launch command, terminal launch arguments, open-file request arguments,
    /// and jump-to-cursor-position request arguments in case any of them is null/not already set.
    /// </summary>
    private static bool SetDefaults()
    {
      if (s_DiscoveredNeovimInstallations.Any())
      {
        s_Config.NvimExecutablePath = s_DiscoveredNeovimInstallations.First().Path;
      }

      string termLaunchCmd = s_Config.TermLaunchCmd;
      string termLaunchArgs = s_Config.TermLaunchArgs;

      // if cmd is empty/whitespace => no terminal launch cmd has been provided/chosen yet
      if (string.IsNullOrWhiteSpace(termLaunchCmd) || string.IsNullOrWhiteSpace(termLaunchArgs))
      {
        // pick the first default available terminal from the list of 'popular' terminal emulators. Obviously this is
        // some sort of a heuristic but the user can explicitly change this through the GUI.
        bool s = false;
        foreach (var termLaunch in TemplateCollection.TermLaunchCmdTemplates)
        {
          if (TryChangeTermLaunchCmd(termLaunch.Item1, termLaunch.Item2))
          {
            s = true;
            break;
          }
        }
        // no available terminal is found from the 'most common' term list
        if (!s)
        {
          // you can't show a GUI window here -- so just log a warning
          Debug.LogError($"[neovim.ide] no valid terminal launcher is available. " +
              "You have to set the terminal launch command by going to the menu item: Neovim => Settings");
          // TODO: open settings menu
          return false;
        }
      }

      if (!s_Config.ModifierBindings.Any() && !string.IsNullOrWhiteSpace(s_Config.OpenFileArgs))
      {
        s_Config.ModifierBindings.Add(new ModifierBinding { Modifiers = 0, Args = s_Config.OpenFileArgs });
        s_Config.SetDirty(true);
        s_Config.Save();
      }

      if (!s_Config.ModifierBindings.Any())
      {
        if (!TemplateCollection.OpenFileArgTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] open-file template list is empty");
        }
        s_Config.ModifierBindings = new System.Collections.Generic.List<ModifierBinding> {
          new ModifierBinding() { Modifiers = 0, Args = TemplateCollection.OpenFileArgTemplates[0].Args }
        };
        s_Config.Save();
      }

      if (string.IsNullOrWhiteSpace(s_Config.JumpToCursorPositionArgs))
      {
        if (!TemplateCollection.JumpToCursorPositionArgTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] the jump-to-cursor-position arguments templates array is empty");
        }
        s_Config.JumpToCursorPositionArgs = TemplateCollection.JumpToCursorPositionArgTemplates[0].Args;
        s_Config.Save();
      }

      return true;
    }


    public static string GetNeovimVersion(string p)
    {
      // get Neovim installation version
      string version = "v-unknown";
      using (var proc = ProcessUtils.HeadlessProcess())
      {
        proc.StartInfo.FileName = p;
        proc.StartInfo.Arguments = "--version";
        proc.RunWithAssertion(s_Config.ProcessTimeout);
        var line = proc.StandardOutput.ReadLine();
        if (line != null)
        {
          version = line.Substring(line.IndexOf(' ') + 1);
        }
        return version;
      }
    }

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {
      // config initialization
      if (!NeovimEditorConfig.Load(out s_Config))
      {
        Debug.LogWarning("[neovim.ide] couldn't load saved config. Consider reseting the config by going to the "
            + "top menu: Neovim -> Settings -> Reset");
      }
      if (!SetDefaults())
        return;

      // initialize with project regeneration flags from config
      s_Generator = new ProjectGeneration(s_Config.CsprojFlags, s_Config.Analyzers);

      s_DiscoveredNeovimInstallations = DiscoverNeovim();

      // do NOT proceed if there aren't any discovered Neovim installations (i.e., not explicitly supplied in settings
      // and not installed in a common path).
      if (!s_DiscoveredNeovimInstallations.Any())
      {
        Debug.LogWarning("[neovim.ide] no Neovim installation was discovered. Consider explicitly providing an nvim "
            + "executable path via top menu: Neovim -> Settings");
        // TODO: show setting window
        return;
      }

      // we use the first discovered/set nvim installation path
      s_Config.NvimExecutablePath = s_DiscoveredNeovimInstallations.First().Path;

      // Unity may launch multiple separate threads (worker threads) during installation. 
      // To everything work properly, we must register the editor and initialize the Installations field in each worker.
      NeovimCodeEditor editor = new NeovimCodeEditor(s_Generator);
      CodeEditor.Register(editor);

      // However, to avoid duplicating RPC connections, focus providers, etc., we initialize them only
      // on the main thread by checking 'AssetDatabase.IsAssetImportWorkerProcess()' in this method
      InitializeInternal();
    }

    /// <summary>
    /// Performs post-registration initialization for singleton-bound resources.
    /// </summary>
    /// <remarks>
    /// Intended for setting up shared system resources that must only exist in a single instance,
    /// such as the RPC connection, event listeners, and OS window focusing services.
    /// </remarks>
    private static void InitializeInternal()
    {
      // proceed only if in main thread
      if (AssetDatabase.IsAssetImportWorkerProcess())
        return;

      // TODO: MPE

#if UNITY_EDITOR_LINUX
      s_NeovimFocus = new LinuxNeovimWindowFocus();
#elif UNITY_EDITOR_WIN
      s_NeovimFocus = new WindowsNeovimWindowFocus(s_ReadWindowHandlePath);
#else
      s_NeovimFocus = new FallbackNeovimWindowFocus();
#endif

    }

    /// <summary>
    /// Discovers available Neovim installations.
    /// </summary>
    /// <remarks>
    /// Validates the user-configured executable path first. If invalid or not set,
    /// falls back to scanning predefined candidate paths on the system.
    /// </remarks>
    /// <returns>
    /// An array of discovered <see cref="CodeEditor.Installation"/> instances,
    /// or an empty array if no valid installations are found.
    /// </returns>
    private static CodeEditor.Installation[] DiscoverNeovim()
    {
      CodeEditor.Installation[] installations = new CodeEditor.Installation[0];

      // if nvim executable path is already set in the config - check if it is still valid
      if (!string.IsNullOrWhiteSpace(s_Config.NvimExecutablePath))
      {
        string v;
        if (File.Exists(s_Config.NvimExecutablePath)
            && (v = GetNeovimVersion(s_Config.NvimExecutablePath)) != "v-unknown")
        {
          installations = new CodeEditor.Installation[] { new CodeEditor.Installation() {
            Name = $"Neovim {v}",
            Path = s_Config.NvimExecutablePath
          }};
        }
        else
        {
          Debug.LogWarning("[neovim.ide] the provided nvim executable path is no longer valid. Falling back to "
              + "automated nvim installation discovery (consider updating path via top menu: Neovim -> Settings ).");
          s_Config.NvimExecutablePath = null;
        }
      }

      // initialize the discovered Neovim installations array. The first 'path' is usually set to "nvim"
      // (or "nvim.exe"). That is obviously not a path but the expected name of Neovim on PATH (which is what the
      // CmdPath does here).
      if (!installations.Any())
      {
        installations = s_CandidateNeovimPaths
          .Select(p => p = Path.IsPathRooted(p) ? p : ProcessUtils.CmdPath(p, s_Config.ProcessTimeout))
          .Where(p => p != null && File.Exists(p))
          .Select(p =>
          {
            return new CodeEditor.Installation
            {
              Name = $"Neovim {GetNeovimVersion(p)}",
              Path = p,
            };
          })
          .ToArray();
      }

      return installations;
    }


    public void CreateIfDoesntExist()
    {
      s_Generator.Sync();
    }

    public static bool TryChangeTermLaunchCmd(string cmd, string args, string env = "")
    {
      if (cmd.Contains("{app}"))  // in case the Neovim executable is invoked directly
      {
        if (!File.Exists(s_Config.NvimExecutablePath))
          return false;
        cmd = cmd.Replace("{app}", s_Config.NvimExecutablePath);
      }
      else  // or through terminal
      {
        if (Path.IsPathRooted(cmd))
        {
          if (!File.Exists(cmd))
            return false;
        }
        else if (ProcessUtils.CmdPath(cmd, s_Config.ProcessTimeout) == null)
          return false;
      }

      // serialize the new terminal launch command in Unity Editor's preferences settings
      s_Config.TermLaunchCmd = cmd;
      s_Config.TermLaunchArgs = args;
      s_Config.TermLaunchEnv = env;

#if UNITY_EDITOR_WIN
      s_Config.PrevServerSocket = string.Empty;
#endif

      s_Config.Save();
      return true;
    }

    private static readonly CodeEditor.Installation[] s_DiscoveredNeovimInstallations = new CodeEditor.Installation[0];
    public CodeEditor.Installation[] Installations => s_DiscoveredNeovimInstallations;


    public NeovimCodeEditor(IGenerator projectGeneration)
    {
      s_Generator = projectGeneration;
    }


    // Callback to the IExternalCodeEditor when it has been chosen from the PreferenceWindow.
    public void Initialize(string editorInstallationPath) { }


    // Unity stores the path of the chosen editor. An instance of
    // IExternalCodeEditor can take responsibility for this path, by returning
    // true when this method is being called. The out variable installation need
    // to be constructed with the path and the name that should be shown in the
    // "External Tools" code editor list.
    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
      editorPath = Path.GetFullPath(editorPath);
      installation = s_DiscoveredNeovimInstallations.FirstOrDefault(i => i.Path == editorPath);
      return !Equals(installation, default(CodeEditor.Installation));
    }

    /// <summary>
    /// Tries to add the provided analyzer. If successful, the underlying config is serialized and the project
    /// generator is syncronized.
    /// </summary>
    public static bool TryAddAnalyzer(string path)
    {
      if (s_Config.TryAddAnalyzer(path))
      {
        // Debug.Log($"[neovim.ide] added analyzer: {Path.GetFileName(path)}");
        s_Config.Save();
        Sync();
        return true;
      }
      return false;
    }

    /// <summary>
    /// Deletes analyzer at provided index, serializes the underlying config, and syncs the generator.
    /// </summary>
    public static void DelAnalyzerAt(int i)
    {
      s_Config.DelAnalyzerAt(i);
      s_Config.Save();
      s_Generator.Sync();
    }

    /// <summary>
    /// Reset the Neovim configuration by deleting the saved EditorPrefs and re-initializing.
    /// Use this when settings become corrupted or you want to start fresh.
    /// </summary>
    public static void ResetConfig()
    {
      NeovimEditorConfig.Reset();
      if (!NeovimEditorConfig.Load(out s_Config))
      {
        Debug.LogWarning("[neovim.ide] couldn't load saved config. Consider reseting the config via going to the "
            + "top menu then: Neovim -> Settings -> Reset");
      }

      // set some defaults in case they are not already set (launch cmd and args, open-file args, etc.)
      if (!SetDefaults())
        return;

      // sync deserialized analyzers with the project generator's analyzers
      s_Generator.SetAnalyzers(s_Config.Analyzers);
      s_Generator.AssemblyNameProvider.CsprojFlags = s_Config.CsprojFlags;
      s_Generator.Sync();
    }

    // Unity calls this method when it populates "Preferences/External Tools"
    // in order to allow the code editor to generate necessary GUI. For example,
    // when creating an an argument field for modifying the arguments sent to
    // the code editor.
    public void OnGUI()
    {
      // internal bug in some Unity versions cause the call NeovimSettingsWindow.ShowWindow() to generate:
      // "EndLayoutGroup: BeginLayoutGroup must be called first" errors ...
      EditorGUILayout.HelpBox(
        "Configure all Neovim-specific settings by going to the top menu: Neovim => Settings",
        MessageType.Info
      );
    }


    public static ProjectGenerationFlag CsprojFlags
    {
      get => s_Config.CsprojFlags;
      set
      {
        if (value == s_Config.CsprojFlags)
          return;
        s_Config.CsprojFlags = s_Generator.AssemblyNameProvider.CsprojFlags = value;
        s_Generator.Sync();
      }
    }


    // When you change Assets in Unity, this method for the current chosen
    // instance of IExternalCodeEditor parses the new and changed Assets.
    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles,
        string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
      s_Generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(),
          importedFiles);
    }


    // Unity calls this function during initialization in order to sync the
    // Project. This is different from SyncIfNeeded in that it does not get a
    // list of changes.
    public void SyncAll()
    {
      AssetDatabase.Refresh();
      s_Generator.Sync();
    }

    public static void Sync()
    {
      AssetDatabase.Refresh();
      s_Generator.Sync();
    }


    /// <summary>
    /// Checks if an nvim server instance is currently running (for this or previous Unity session) by checking
    /// whether the current server socket is live.
    /// </summary>
    public static bool IsNvimServerInstanceAlreadyRunning()
    {
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      // Connect to the domain socket rather than checking file existence — a stale socket file is
      // left behind when Neovim crashes, which would otherwise cause a false positive.
      // IsUnixSocketAlive also deletes the file if the socket is stale.
      return NetUtils.IsUnixSocketAlive(s_ServerSocket);
#else  // UNITY_EDITOR_WIN
      // this is tricky... using PIDs did not work... domain sockets have an issue on the side of NeoVim...
      // since on Windows we use a randomly available port for the TCP NeoVim server socket, we can know
      // whether a NeoVim server instance is running by trying to bind a TCP listener to the previously used
      // port
      string prevAddr = s_Config.PrevServerSocket;
      if (string.IsNullOrWhiteSpace(prevAddr)) return false;

      int idx = prevAddr.IndexOf(':');
      string ip = prevAddr.Substring(0, idx);
      int port = int.Parse(prevAddr.Substring(idx + 1));
      return NetUtils.IsPortInUse(ip, port);
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="app"></param>
    /// <param name="filePath"></param>
    /// <returns>whether the nvim server instance is successfully instantied.</returns>
    private bool TryInstantiateNvimServerInstance(string app, string filePath)
    {
      try
      {
        using (var p = new Process())
        {
          p.StartInfo.FileName = s_Config.TermLaunchCmd
            .Replace("{app}", app);
          p.StartInfo.Arguments = s_Config.TermLaunchArgs
            .Replace("{app}", app)
            .Replace("{filePath}", string.IsNullOrWhiteSpace(filePath) ? "" : $"\"{filePath}\"")
            .Replace("{serverSocket}", s_ServerSocket)
            .Replace("{instanceId}", s_InstanceId)
            .Replace("{projectRootDir}", FileUtility.NormalizeWindowsToUnix(Directory.GetParent(Application.dataPath).ToString()))
            .Replace("{analyzerDiagnosticScope}", s_Config.AnalyzerDiagnosticScope.ToString())
            .Replace("{compilerDiagnosticScope}", s_Config.CompilerDiagnosticScope.ToString())
#if UNITY_EDITOR_WIN
            .Replace("{getProcessPPIDScriptPath}", s_GetProcessWindowHandlePath)
#endif
          ;

          // pass optionally-set environment variables to process
          if (!string.IsNullOrWhiteSpace(s_Config.TermLaunchEnv))
          {
            foreach (var env in s_Config.TermLaunchEnv.Split(' '))
            {
              var envKey = env.Split('=');
              if (envKey.Length == 2)
              {
                p.StartInfo.Environment[envKey[0]] = envKey[1];
              }
              else
              {
                Debug.LogWarning($"[neovim.ide] failed to parse environment variable entry from: {env}. Expected format is: ENV=VALUE");
              }
            }
          }

          p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
          p.StartInfo.CreateNoWindow = false;
          p.StartInfo.UseShellExecute = false;
          // Debug.Log($"{p.StartInfo.FileName} {p.StartInfo.Arguments}");

          // start and do not care (do not wait for exit)
          p.Start();


#if UNITY_EDITOR_WIN
          // save the server socket so that we can communicate with it later
          // (e.g., when Unity exits but the server is still running)
          s_Config.PrevServerSocket = s_ServerSocket;
          s_Config.Save();

          (s_NeovimFocus as WindowsNeovimWindowFocus)?.TryGetWindowHandle(p);
#endif
          return true;
        }
      }
      catch (Exception e)
      {
        Debug.LogError($"[neovim.ide] failed to create a Neovim server instance. Reason: {e}");
        return false;
      }
    }


    /// <summary>
    /// Tries to open the provided <paramref name="filePath"> in Neovim by sending a request to the currently running
    /// Neovim server instance.
    /// </summary>
    /// <param name="app">Neovim executable</param>
    /// <param name="filePath">absolute path of the file to open</param>
    private void TryOpenFileInNvimServerInstance(string app, string filePath)
    {
      // send request to Neovim server instance listening on the provided socket path to open a tab/buffer corresponding
      // to the provided filepath. Skip when filePath is empty (e.g., "Assets/Open C# project").
      if (string.IsNullOrWhiteSpace(filePath))
        return;
      int currentMods = Event.current != null ? (int)Event.current.modifiers : 0;
      const int relevantMask = (int)(EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt);
      currentMods &= relevantMask;

      var binding = s_Config.ModifierBindings
        .FirstOrDefault(b => (b.Modifiers & relevantMask) == currentMods)
        ?? s_Config.ModifierBindings.FirstOrDefault(b => b.Modifiers == 0);

      string openFileArgs = binding?.Args ?? TemplateCollection.OpenFileArgTemplates[0].Args;

      string args = openFileArgs
        .Replace("{serverSocket}", s_ServerSocket)
        .Replace("{filePath}", $"\"{filePath}\"");

      using (var p = ProcessUtils.HeadlessProcess())
      {
        p.StartInfo.FileName = app;
        p.StartInfo.Arguments = args;
#if UNITY_EDITOR_WIN
        // on Windows, for some reason the process executes correctly but without exiting within any given timeout
        // to fix that, we simply catch the TimeoutException and kill the process.
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (TimeoutException) { }
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        // life is ez on Linux (unless you deal with any window manager...)
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (ExitCodeMismatchException e)
        {
          Debug.LogWarning($"[neovim.ide] failed to open file in Neovim server. Exit code: {e.Actual}. Is the server running?");
        }
        catch (TimeoutException) { }
#endif
      }
    }


    /// <summary>
    /// Tries to jump to provided cursor position and fails silently in case it can't. If <paramref name="line"> is set
    /// to 1 and <paramref name="column"> is set to 0 this function does nothing.
    /// </summary>
    /// <param name="app">Nvim executable</param>
    /// <param name="line">line to jump to.</param>
    /// <param name="column">column to jump to.</param>
    /// <returns></returns>
    private void TryJumpToCursorPosition(string app, int line, int column)
    {
      /*
      * now send request to jump cursor to exact position. You cannot do both --remote-tab and --remote-send at the
      * same time (this is a limitation of the Neovim CLI as it will only execute the last --remote argument and not
      * both)
      */
      if (line == 1 && column == 0)
        return;
      string args = s_Config.JumpToCursorPositionArgs
        .Replace("{serverSocket}", s_ServerSocket)
        .Replace("{line}", line.ToString())
        .Replace("{column}", column.ToString());

      using (var p = ProcessUtils.HeadlessProcess())
      {
        p.StartInfo.FileName = app;
        p.StartInfo.Arguments = args;
#if UNITY_EDITOR_WIN
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (TimeoutException) { }
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (ExitCodeMismatchException) { }
        catch (TimeoutException) { }
#endif
      }
    }

    /// <summary>
    /// The external code editor needs to handle the request to open a file. Note that by returning 'false' Unity will
    /// try to open the file in a different program which is the reason why, for instance, we return 'false' for image
    /// files and other formats that are not expected not to be opened by Neovim.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="line"></param>
    /// <param name="column"></param>
    /// <returns>true in case Neovim managed to successfully open this project/file. false, in case the filetype is
    /// not execpted to be opened by Neovim (e.g., images) or in case this fails to open the project/file in Neovim.
    /// </returns>
    public bool OpenProject(string filePath = "", int line = -1, int column = -1)
    {
      if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath)) return false;
      if (line == -1) line = 1;
      if (column == -1) column = 0;

      // only use NeoVim for reasonable file extensions (e.g., do not use NeoVim to open .png files which happens
      // without this check). Skip extension check when filePath is empty (e.g., "Assets/Open C# project").
      if (!string.IsNullOrWhiteSpace(filePath) && !Array.Exists(s_SupportedExtensions, e => e.ToLower() == Path.GetExtension(filePath)
            .TrimStart('.')
            .ToLower()))
        return false;

#if UNITY_EDITOR_WIN
      string app = $"\"{s_Config.NvimExecutablePath}\"";
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      string app = s_Config.NvimExecutablePath;
#endif

      // instantiate a new Neovim server instance in case there isn't one running for this Unity session
      if (!IsNvimServerInstanceAlreadyRunning())
        if (!TryInstantiateNvimServerInstance(app, filePath))
          return false;

#if UNITY_EDITOR_WIN
      // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not available for socket type"
      // so we have to listen to a TCP socket instead with a local addr and a random port - this will be overwitten below
      s_ServerSocket = s_Config.PrevServerSocket;
#endif

      TryOpenFileInNvimServerInstance(app, filePath);

      // optionally jump to cursor position
      TryJumpToCursorPosition(app, line, column);

      // optionally focus on Neovim server instance window - this is extremely tricky to implement across platforms
      s_NeovimFocus.Focus();

      return true;
    }


    public static RoslynDiagnosticScope SetAnalyzerDiagnosticScope(RoslynDiagnosticScope scope)
    {
      s_Config.AnalyzerDiagnosticScope = scope;
      SendNeovimCmd($":lua _G.nvim_unity_analyzer_diagnostic_scope='{s_Config.AnalyzerDiagnosticScope}'<CR>");
      return s_Config.AnalyzerDiagnosticScope;
    }

    public static RoslynDiagnosticScope SetCompilerDiagnosticScope(RoslynDiagnosticScope scope)
    {
      s_Config.CompilerDiagnosticScope = scope;
      SendNeovimCmd($":lua _G.nvim_unity_compiler_diagnostic_scope='{s_Config.CompilerDiagnosticScope}'<CR>");
      return s_Config.CompilerDiagnosticScope;
    }


    /// <summary>
    /// Sends a remote command to the currenly running Neovim server instance.
    /// </summary>
    public static void SendNeovimCmd(string cmd)
    {
#if UNITY_EDITOR_WIN
      string app = $"\"{s_Config.NvimExecutablePath}\"";
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      string app = s_Config.NvimExecutablePath;
#endif
      using (var p = ProcessUtils.HeadlessProcess())
      {
        p.StartInfo.FileName = app;
        p.StartInfo.Arguments = $"--server {s_ServerSocket} --remote-send \"{cmd}\"";
#if UNITY_EDITOR_WIN
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (TimeoutException) { }
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (ExitCodeMismatchException) { }
        catch (TimeoutException) { }
#endif
      }
    }


    /// <summary>
    /// Sends a remote command to the currenly running Neovim server instance to restart Roslyn LS.
    /// </summary>
    public static void RestartRoslynLS() => SendNeovimCmd($":source {s_RestartRoslynLSPath}<CR>");


  }
}
