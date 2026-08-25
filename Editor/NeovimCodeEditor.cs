#pragma warning disable IDE0130, IDE0300, IDE0090, IDE0063, IDE0057
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;


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

    private static readonly ConcurrentQueue<Action> s_ExecutionQueue = new ConcurrentQueue<Action>();
    private static readonly NeovimCodeEditor s_NeovimCodeEditor = null;
    private static IGenerator s_Generator = null;
    private static INeovimWindowFocus s_NeovimFocus = null;
    private static NeovimRpcClient s_RpcClient = null;

    private const double k_ConnectionTimeout = 2d;
    private const double k_ConnectionAttemptPause = 400d;

    private static bool s_ConnectionPending = false;
    private static double s_ConnectionLastAttemptTime = 0d;
    private static double s_ConnectionAttemptsStartTime = 0d;

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
        s_Config.ModifierBindings.Add(new ModifierBinding { Modifiers = 0, Command = s_Config.OpenFileArgs });
        s_Config.SetDirty(true);
        s_Config.Save();
      }

      if (!s_Config.ModifierBindings.Any())
      {
        if (!TemplateCollection.OpenFileCmdTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] open-file template list is empty");
        }
        s_Config.ModifierBindings = new List<ModifierBinding> {
          new ModifierBinding() { Modifiers = 0, Command = TemplateCollection.OpenFileCmdTemplates[0].Command }
        };
        s_Config.Save();
      }

      if (string.IsNullOrWhiteSpace(s_Config.JumpToCursorPositionArgs))
      {
        if (!TemplateCollection.JumpToCursorPositionCmdTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] the jump-to-cursor-position arguments templates array is empty");
        }
        s_Config.JumpToCursorPositionArgs = TemplateCollection.JumpToCursorPositionCmdTemplates[0].Command;
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
      s_NeovimCodeEditor = new NeovimCodeEditor(s_Generator);
      CodeEditor.Register(s_NeovimCodeEditor);

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

      EditorApplication.quitting += CleanupResources;
      AssemblyReloadEvents.beforeAssemblyReload += CleanupResources;
      EditorApplication.update += Update;

      // Try to connect during initialization. If the connection is successful,
      // nvim is already open; otherwise, it will be initialized when project opens
      TryInitializeRpcClient();
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

    private static bool TryInitializeRpcClient()
    {
      Debug.Log("Try init");

      if (s_RpcClient != null)
      {
        if (s_RpcClient.IsConnected)
          return true;

        DestroyClient();
      }

      try
      {
        var rpcClient = new NeovimRpcClient(s_ServerSocket);
        rpcClient.Connect();
        rpcClient.OnConnectionBreak += OnConnectionBreakHandler;
        s_RpcClient = rpcClient;

        return true;
      }
      catch (Exception)
      {
        Debug.Log("Init failed");
        return false;
      }
    }

    private static bool TryInitializeRpcClientAndExecute(Func<bool> action)
    {
      // try using the existing client
      if (s_RpcClient != null)
      {
        if (action())
          return true;

        Debug.Log("Dest 1");
        DestroyClient();
      }

      // try to (re)init the client
      if (TryInitializeRpcClient())
      {
        Debug.Log("Conn 2");
        if (action())
          return true;

        DestroyClient();
      }

      Debug.Log("exec failed");
      // cant connect, probably nvim isnt running
      return false;
    }

    private static void DestroyClient()
    {
      Debug.Log("Destroy");

      if (s_RpcClient == null)
        return;

      s_RpcClient.OnConnectionBreak -= OnConnectionBreakHandler;
      s_RpcClient.Dispose();
      s_RpcClient = null;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>whether the nvim server instance is successfully instantied.</returns>
    private bool TryInstantiateNvimServerInstance(string filePath, int line = 1, int column = 0)
    {
      Debug.Log("Init instance ");

#if UNITY_EDITOR_WIN
      string app = $"\"{s_Config.NvimExecutablePath}\"";
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      string app = s_Config.NvimExecutablePath;
#endif

      try
      {
        using (var p = new Process())
        {
          p.StartInfo.FileName = s_Config.TermLaunchCmd
            .Replace("{app}", app);
          p.StartInfo.Arguments = s_Config.TermLaunchArgs
            .Replace("{app}", app)
            .Replace("{filePath}", string.IsNullOrWhiteSpace(filePath) ? "" : $"\"{filePath}\"")
            .Replace("{line}", line.ToString())
            .Replace("{column}", column.ToString())
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

          // NOTE: Because it takes some time to init socket, we cannot create RPC connection immediately after nvim starts.
          // To avoid complicated async stuff, we simply tie connection attempts to Unity's update method
          s_ConnectionPending = true;
          s_ConnectionAttemptsStartTime = EditorApplication.timeSinceStartup;

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

    private bool TryOpenFileViaRpc(string filePath, int line = 1, int column = 0)
    {
      Debug.Log($"rpc open [{line}, {column}] {filePath}");

      List<string> commands = new();
      if (!string.IsNullOrWhiteSpace(filePath))
      {
        int currentMods = Event.current != null ? (int)Event.current.modifiers : 0;
        const int relevantMask = (int)(EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt);
        currentMods &= relevantMask;

        // TODO: add multiple commands support
        var binding = s_Config.ModifierBindings
          .FirstOrDefault(b => (b.Modifiers & relevantMask) == currentMods)
          ?? s_Config.ModifierBindings.FirstOrDefault(b => b.Modifiers == 0);

        string openFileCmd = binding?.Command ?? TemplateCollection.OpenFileCmdTemplates[0].Command;
        // NOTE: nvim commands like ':drop' dont accept quoted paths, so we must escape them with '\'.
        // Cuz it is sent directly to Neovim, this operation is terminal independent.
        string cmdOpen = openFileCmd.Replace("{filePath}", filePath.NormalizeWindowsToUnix().Replace(" ", "\\ "));

        commands.Add(cmdOpen);

        // call the user specified jump command if needed
        if (line != 1 || column != 0)
        {
          string cmdJump = s_Config
            .JumpToCursorPositionArgs
            .Replace("{line}", line.ToString())
            .Replace("{column}", column.ToString());

          commands.Add(cmdJump);
        }
      }
      else
      {
        // Just ping the client to ensure its alive when filePath is empty (e.g., "Assets/Open C# project").
        commands.Add("echon ''");
      }

      // NOTE: sending commands seperately can cause a race condition within neovim itself (eg. the autocmd
      // to restore the last cursor position is called between the ':drop file' and 'call cursor()' commands)
      return TryInitializeRpcClientAndExecute(() => SendCommandsAtomic(commands));
    }

    private bool SendCommandsAtomic(IEnumerable<string> commands)
    {
      if (s_RpcClient == null)
        return false;

      var vimscript = string.Join('\n', commands);

      switch (s_RpcClient.NvimExec2(vimscript))
      {
        case InvokeResult.NetworkError:
          Debug.Log("net errr");
          return false;
        case InvokeResult.LogicError:
          Debug.LogError($"[neovim.ide] Failed to execute neovim commands: {vimscript}");
          break;
      }

      return true;
    }

    // send request to Neovim server instance listening on the provided socket path
    private bool SendCommand(string command)
    {
      Debug.Log($"sending cmd {command}");
      if (s_RpcClient == null)
        return false;

      switch (s_RpcClient.NvimCommand(command))
      {
        // NetworkError means something wrong with connection\socket\client
        case InvokeResult.NetworkError:
          Debug.Log("net errr");
          return false;
        // LogicError means something wrong with command\code\other unexpected error
        case InvokeResult.LogicError:
          Debug.LogError($"[neovim.ide] Failed to execute neovim command: {command}");
          break;
      }

      return true;
    }

    private static void OnConnectionBreakHandler()
    {
      Debug.Log("Connection broken");

      DestroyClient();
    }

    private static void CleanupResources()
    {
      EditorApplication.quitting -= CleanupResources;
      EditorApplication.update -= Update;
      AssemblyReloadEvents.beforeAssemblyReload -= CleanupResources;

      DestroyClient();
    }

    private static void Update()
    {
      ConnectionAttemptTick();

      // process actions from background threads
      while (s_ExecutionQueue.TryDequeue(out var action))
      {
        try
        {
          action.Invoke();
        }
        catch (Exception ex)
        {
          Debug.LogError(
            $"[ExternalEditorPlugin] Exception in dispatched action: {ex.Message}\n{ex.StackTrace}"
          );
          // TODO: err msg
        }
      }
    }

    private static void ConnectionAttemptTick()
    {
      if (!s_ConnectionPending)
        return;
      Debug.Log("con tick");

      var currentTime = EditorApplication.timeSinceStartup;
      var timePassed = currentTime - s_ConnectionLastAttemptTime;
      if (timePassed <= k_ConnectionAttemptPause)
      {
        Debug.Log("con pause not");
        return;
      }

      s_ConnectionLastAttemptTime = currentTime;
      if (TryInitializeRpcClient())
      {
        Debug.Log("con init true");
        s_ConnectionPending = false;
        return;
      }

      if (currentTime - s_ConnectionAttemptsStartTime >= k_ConnectionTimeout)
      {
        Debug.Log("con timeout");
        s_ConnectionPending = false;
      }
    }

    public static void EnqueueAction(Action action)
    {
      s_ExecutionQueue.Enqueue(action);
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
      Debug.Log($"Open project [{line}, {column}] {filePath}");

      if (!string.IsNullOrWhiteSpace(filePath))
      {
        if (!File.Exists(filePath))
          return false;

        // only use NeoVim for reasonable file extensions (e.g., do not use NeoVim to open .png files which happens
        // without this check). Skip extension check when filePath is empty (e.g., "Assets/Open C# project").
        if (
          !Array.Exists(
            s_SupportedExtensions,
            e => string.Equals( e, Path.GetExtension(filePath).TrimStart('.'), StringComparison.OrdinalIgnoreCase)
          )
        )
        {
          return false;
        }
      }

      if (line == -1) line = 1;
      if (column == -1) column = 0;

      if (TryOpenFileViaRpc(filePath, line, column))
      {
        s_NeovimFocus.Focus();
        return true;
      }

      // if rpc command failed - nvim isnt running
      var initRes = TryInstantiateNvimServerInstance(filePath);

      // TODO: ????
#if UNITY_EDITOR_WIN
      // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not available for socket type"
      // so we have to listen to a TCP socket instead with a local addr and a random port - this will be overwitten below
      s_ServerSocket = s_Config.PrevServerSocket;
#endif

      return initRes;
    }


    public static RoslynDiagnosticScope SetAnalyzerDiagnosticScope(RoslynDiagnosticScope scope)
    {
      s_Config.AnalyzerDiagnosticScope = scope;
      TryInitializeRpcClientAndExecute(() =>
        s_NeovimCodeEditor.SendCommand(
          $":lua _G.nvim_unity_analyzer_diagnostic_scope='{s_Config.AnalyzerDiagnosticScope}'<CR>"
        )
      );
      return s_Config.AnalyzerDiagnosticScope;
    }

    public static RoslynDiagnosticScope SetCompilerDiagnosticScope(RoslynDiagnosticScope scope)
    {
      s_Config.CompilerDiagnosticScope = scope;
      TryInitializeRpcClientAndExecute(() =>
        s_NeovimCodeEditor.SendCommand(
          $":lua _G.nvim_unity_compiler_diagnostic_scope='{s_Config.CompilerDiagnosticScope}'<CR>"
        )
      );
      return s_Config.CompilerDiagnosticScope;
    }

    /// <summary>
    /// Sends a remote command to the currenly running Neovim server instance to restart Roslyn LS.
    /// </summary>
    public static void RestartRoslynLS()
    {
      TryInitializeRpcClientAndExecute(() =>
        s_NeovimCodeEditor.SendCommand(
          $"source {s_RestartRoslynLSPath.NormalizeWindowsToUnix().Replace(" ", "\\ ")}"
        )
      );
    }
  }
}
