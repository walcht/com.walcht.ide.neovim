using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MPack;
using UnityEditor;
using UnityEngine;

namespace Neovim.Editor
{
  public enum InvokeResult
  {
    Success,
    NetworkError,
    LogicError,
  }

  public class NeovimRpcClient : IDisposable
  {
    private readonly string m_SetupUnityRPCPath = Path.GetFullPath(
      "Packages/com.walcht.ide.neovim/SetupUnityRPC.lua"
    );

    private Stream m_Stream;
    private bool m_IsDisposed;
    private uint m_CurrentMsgId;
    private CancellationTokenSource m_ListenCancelationSource;

    public event Action OnConnectionBreak;

    public const string NeovimRpcClientName = "NvimUnityRpc";
    public string ServerSocket { get; }
    public bool IsConnected => !m_IsDisposed && m_Stream != null;

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
    private readonly Socket m_UnixSocket;

    public NeovimRpcClient(string serverSocket)
    {
      ServerSocket = serverSocket;

      m_UnixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    }

    // TODO: async connection??
    public void Connect()
    {
      Debug.Log("connect start");

      if (m_IsDisposed)
        throw new ObjectDisposedException(nameof(NeovimRpcClient));

      var endpoint = new UnixDomainSocketEndPoint(ServerSocket);
      m_UnixSocket.Connect(endpoint);

      m_Stream = new NetworkStream(m_UnixSocket);

      m_ListenCancelationSource = new();
      var token = m_ListenCancelationSource.Token;
      Task.Run(() => ListenLoop(token), token);

      IdentifyMyself();

      Debug.Log("connected");
    }
#else // UNITY_EDITOR_WIN
    private readonly string m_Ip;
    private readonly int m_Port;

    private readonly TcpClient m_TcpClient;

    public NeovimRpcClient(string serverSocket)
    {
      var parts = serverSocket.Split(':');
      if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
        throw new ArgumentException("Invalid TCP adress format", nameof(serverSocket));

      ServerSocket = serverSocket;
      m_Ip = parts[0];
      m_Port = port;

      m_TcpClient = new TcpClient();
    }

    public void Connect()
    {
      Debug.Log("connect start");

      if (m_IsDisposed)
        throw new ObjectDisposedException(nameof(NeovimRpcClient));

      m_TcpClient.Connect(m_Ip, m_Port);
      m_Stream = m_TcpClient.GetStream();

      m_ListenCancelationSource = new();
      var token = m_ListenCancelationSource.Token;
      Task.Run(() => ListenLoop(token), token);

      IdentifyMyself();

      Debug.Log("connected");
    }
#endif

    public void Dispose()
    {
      if (m_IsDisposed)
        return;

      m_IsDisposed = true;

      m_ListenCancelationSource?.Cancel();

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      m_UnixSocket.Dispose();
#else
      m_TcpClient.Dispose();
#endif

      m_Stream?.Dispose();

      m_ListenCancelationSource?.Dispose();
    }

    public void SendRequest(string method, object[] args)
    {
      if (m_IsDisposed)
        throw new ObjectDisposedException(nameof(NeovimRpcClient));

      var request = new MArray
      {
        MToken.From(0),
        MToken.From(m_CurrentMsgId++),
        MToken.From(method),
        MToken.From(args),
      };

      request.EncodeToStream(m_Stream);
      m_Stream.Flush();
    }

    public InvokeResult NvimCommand(string command)
    {
      return ExecuteSafe(() =>
      {
        SendRequest("nvim_command", new object[] { command });
      });
    }

    // nvim_exec2()
    // captureOutput - boolean, whether to capture and return the script's output
    public InvokeResult NvimExec2(string vimscript, bool captureOutput = false)
    {
      return ExecuteSafe(() =>
      {
        SendRequest(
          "nvim_exec2",
          new object[]
          {
            vimscript,
            new MDict() { { "output", captureOutput } },
          }
        );
      });
    }

    public InvokeResult NvimExecLua(string code, object[] args)
    {
      return ExecuteSafe(() =>
      {
        SendRequest("nvim_exec_lua", new object[] { code, args });
      });
    }

    private InvokeResult ExecuteSafe(Action rpcAction)
    {
      try
      {
        rpcAction();
        return InvokeResult.Success;
      }
      catch (IOException)
      {
        return InvokeResult.NetworkError;
      }
      catch (ObjectDisposedException)
      {
        return InvokeResult.NetworkError;
      }
      catch (Exception ex)
      {
        Debug.LogError($"[neovim.ide] RPC request failed due to unknown error: {ex.Message}");
        return InvokeResult.LogicError;
      }
    }

    private void IdentifyMyself()
    {
      // set the channel info
      SendRequest(
        "nvim_set_client_info",
        new object[]
        {
          NeovimRpcClientName, // name
          new MDict(), // version, left empty
          "remote", // type
          new MDict(), // methods
          new MDict(), // attributes
        }
      );

      // run the setup script
      SendRequest(
        "nvim_command",
        new object[]
        {
          $"source {m_SetupUnityRPCPath.NormalizeWindowsToUnix().Replace(" ", "\\ ")}",
        }
      );
    }

    private async void ListenLoop(CancellationToken ct)
    {
      try
      {
        while (!ct.IsCancellationRequested)
        {
          var message = await MToken.ParseFromStreamAsync(m_Stream, ct);
          var type = message[0].To<int>();

          switch (type)
          {
            case 0:
              HandleRequest(message);
              break;
            case 1:
              HandleResponse(message);
              break;
            case 2:
              HandleNotification(message);
              break;
          }
        }
      }
      // IOException is usually thrown when:
      // 1. object is being disposed or\and cancellation was requested (_isDisposed == true)
      catch (IOException)
      {
        Debug.Log("IOEx");
        if (!m_IsDisposed && !ct.IsCancellationRequested)
        {
          Debug.Log("Connect broke");
          OnConnectionBreak?.Invoke();
        }
      }
      // InvalidDataException is usually thrown when:
      // 1. stream was clossed (eg. user closed nvim)
      catch (InvalidDataException)
      {
        Debug.Log("InvalidDataException");
        if (!m_IsDisposed && !ct.IsCancellationRequested)
        {
          Debug.Log("Connect broke");
          OnConnectionBreak?.Invoke();
        }
      }
      // we dont expect any other exceptions, but if one occurs, we must notify about broken connection
      catch (Exception e)
      {
        Debug.LogError($"err: {e.GetType().Name}, {e.Message}");

        OnConnectionBreak?.Invoke();
      }
    }

    private void HandleRequest(MToken message)
    {
      // TODO:
    }

    private void HandleResponse(MToken message)
    {
      var isSuccess = message[2].IsNull();
      if (isSuccess)
        return;

      var msgId = message[1].To<uint>();
      var error = message[2][1].To<string>();

      Debug.LogError(
        $"[neovim.ide] Recieved an error reponce for message '{msgId}' from Neovim: {error}"
      );
    }

    private void HandleNotification(MToken message)
    {
      var method = message[1].To<string>();

      switch (method)
      {
        // TODO: add UnityAssetMoved notification
        case "UnityAssetChanged":
          // case "UnityAssetCreated":
          UnityAssetChangedHandler(message[2][0].To<string>());
          break;
        case "UnityAssetCreated":
          UnityAssetCreatedHandler(message[2][0].To<string>());
          break;
        case "UnityAssetDeleted":
          UnityAssetDeletedHandler(message[2][0].To<string>());
          break;
        case "UnitySyncAll":
          UnitySyncAllHandler();
          break;
        case "UnityFocus":
          UnityFocusHandler();
          break;
      }
    }

    private void UnityAssetChangedHandler(string absolutePath)
    {
      var projectPath = FileUtility.MakeRelativeToProjectPath(absolutePath);

      if (projectPath == null)
        return;

      // NOTE: We cant call Unity api, such as ImportAsset, from a background thread.
      // Using EditorApplication.delayCall will only fire when Unity gains focus.
      // To avoid these issues we queue the actions in a thread-safe queue
      // and execute them on the main thread in the Update method
      NeovimCodeEditor.EnqueueAction(() =>
      {
        AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.Default);

        Debug.Log($"[Neovim.Unity] Auto-imported: {projectPath}");
      });
    }

    private void UnityAssetCreatedHandler(string absolutePath)
    {
      // because moving\renaming files without deleting their .meta files results in compilation errors,
      // it is safer to perform a full Refresh() rather than importing a separate file
      NeovimCodeEditor.EnqueueAction(() =>
      {
        AssetDatabase.Refresh();

        Debug.Log($"[Neovim.Unity] Auto-imported: {absolutePath}");
      });
    }

    private void UnityAssetDeletedHandler(string absolutePath)
    {
      NeovimCodeEditor.EnqueueAction(() =>
      {
        // you can delete a single asset with DeleteAsset(path)
        // but you cant do this, if the file has already been deleted... so the ghost asset will remain in the database...
        // to minimize overhead, we can reimport the parent directory of the deleted asset
        // upd: recursive reimport of directories close to the project root turns out to be slover, than calling Refresh() (at least at recent versions Unity)
        // so just Refresh()...
        AssetDatabase.Refresh();
        Debug.Log($"deleted by Refresh: {absolutePath}");
      });
    }

    private void UnitySyncAllHandler()
    {
      NeovimCodeEditor.EnqueueAction(() =>
      {
        AssetDatabase.Refresh();

        Debug.Log($"[Neovim.Unity] Refresh");
      });
    }

    private void UnityFocusHandler()
    {
      NeovimCodeEditor.EnqueueAction(() =>
      {
        // hack - focus EditorWindow (any) window
        // if this causes any bugs - should use a specific window as SceneView
        EditorWindow.FocusWindowIfItsOpen<EditorWindow>();
      });
    }
  }
}
