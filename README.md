# About

Neovim integration with the Unity game engine. Inspired from the official Visual
Studio editor IDE package: [com.unity.ide.visualstudio][com.unity.ide.visualstudio].

## Features

- .csproj generation for LSP purposes
- auto focusing on Neovim server instance window (currently only on Linux GNOME)

## Installation

In the Unity Editor, in the top menu bar navigate to:

Window -> Package Management -> Package Manager -> navigate to plus sign on top left ->
Install package from git URL... -> then paste:

```
https://github.com/walcht/com.walcht.ide.neovim.git
```

### Optional Dependencies for Neovim Window Focus

On Windows, no additional dependencies are needed to switch focus to Neovim window.

On GNOME desktop environments (Ubuntu, Debian, Fedora, etc.), upon starting opening
a C# script from Unity for the first time, you will be prompted to install the
[activate-window-by-title][activate-window-by-title] gnome-extension. You have to
logout then login for the extension to be fully installed. This extension
is crucial for focusing on a window instance based on title on GNOME desktop
environments.

## Usage

Make sure that **Neovim > 0.11** is installed and is globally accessible (i.e.,
added to PATH under the name "nvim")

To automatically open Neovim when clicking on files/console warnings or errors,
Edit -> Preferences -> External Tools -> Set "External Script Editor" to Neovim
-> Adjust which packages to generate the .csproj files for (you will only get
LSP functionalities for those selected packages):

<img width="521" height="258" alt="Unity's external tools menu" src="https://github.com/user-attachments/assets/42bc9118-8e38-4991-8c3d-036fb6b303bc" />

## Change Terminal Emulator Launch Command

By default this package tries to find a default terminal emulator from a small
list of *most-common* terminals (e.g., gnome-terminal, alacritty, etc.). Of
course, if you want to supply by yourself which terminal emulator launch command
to use for launching a new Neovim server instance then you can do so via the
top menu option in the Unity Editor: `Neovim -> Change Terminal Launch Cmd` which
will show the following popup:

<img width="605" height="262" alt="image" src="https://github.com/user-attachments/assets/a3cb103a-2c11-4435-9f16-330671bdbcd7" />

Where:
- `{app}` -- is replaced by the current editor path (i.e., neovim path).
- `{filePath}` -- is replaced by the path to the requested file to be opened by
  Neovim.
- `{serverSocket}` -- is replaced by the path to the IPC socket between
  the Neovim server instance and the client that will send commands. On Linux, this
  is replaced by default to `/tmp/nvimsocket`. On Windows, this is replaced by
  default to `127.0.0.1:<RANDOM-PORT>` (with <RANDOM-PORT> a randomly chosen
  available port).

On Linux, it is advised to set the window name using something like:
`--title "nvimunity"`. this is important for auto window focusing on GNOME.
This, for instance, instructs gnome-terminal to name the newly opened window as
`nvimunity`. This is crucial for focusing on the Neovim server instance
when opening a file from Unity. Change this according to your terminal emulator
but keep the new window name as `nvimunity`.

## Change Open-File Request Args

By default this package uses `--server {serverSocket} --remote-tab {filePath}`
as arguments for process execution when a request to open a file is instantiated
(i.e., replaces args here: `{app} {args}` where `{app}` is the current editor
executable path (i.e., Neovim path)). You can change this by using the top
menu option in the Unity Editor: `Neovim -> Change Open-File Request Args`
which will show the following popup:

<img width="607" height="237" alt="image" src="https://github.com/user-attachments/assets/5f056048-a34c-4f70-9e07-b64e6d5f9287" />

Where:
- `{filePath}` -- is replaced by the path to the requested file to be opened by
  Neovim.
- `{serverSocket}` -- is replaced by the path to the IPC socket between
  the Neovim server instance and the client that will send commands. On Linux, this
  is replaced by default to `/tmp/nvimsocket`. On Windows, this is replaced by
  default to `127.0.0.1:<RANDOM-PORT>` (with <RANDOM-PORT> a randomly chosen
  available port).

## Change Jump-to-Cursor-Position Request Args

By default this package uses
`--server {serverSocket} --remote-send ":call cursor({line},{column})<CR>"` as
arguments for process execution when a request to jump to cursor position is
instantiated. You can change this by using the top menu option in the Unity
Editor: `Neovim -> Change Jump-to-Cursor-Position Args` which will show the
following popup:

<img width="606" height="238" alt="image" src="https://github.com/user-attachments/assets/e1dc2a78-002a-4061-bb23-71b9fdb459d4" />
Where:
- `{serverSocket}` -- is replaced by the path to the IPC socket between
  the Neovim server instance and the client that will send commands. On Linux, this
  is replaced by default to `/tmp/nvimsocket`. On Windows, this is replaced by
  default to `127.0.0.1:<RANDOM-PORT>` (with <RANDOM-PORT> a randomly chosen
  available port).
- `{line}` -- is replaced by the line number that was requested to jump into.
- `{column}` -- is replaced by the column number that was requested to jump into.

## LSP is Not Working for a Particular Package?

If you notice that LSP is not working for a particular package then the most probable
cause is that the `.csproj` for that package wasn't generated. This can be caused by:

- you (:-)) forgetting to enable `.csproj` generation in the `External Tools` menu (
  you have to check, for example, Local pacakges, Git packages, Built-in packages,
  etc.).
- the package not having (or not *correctly* implementing) an `asmdef` file (see [Unity
  asmdef files][unity-asmdef]).

You can troublshoot LSP issues by checking which `.csproj` files are generated and
whether your project's `.sln` was generated.

E.g., simply navigate to your Unity root project's directory and:

```bash
ll | grep ".*\.sln\|.*\.csproj" --color=never
```

This is an example output that I get for a trivial project named
`NeovimIntegrationTest` with packages from Git, local disk, etc. (notice
the `NeovimIntegrationTest.sln` file):

```bash
-rw-rw-r--  1 walcht walcht    72828 Sep 20 20:23 Assembly-CSharp.csproj
-rw-rw-r--  1 walcht walcht    85604 Sep 20 20:23 Assembly-CSharp-Editor.csproj
-rw-rw-r--  1 walcht walcht    71797 Aug  8 00:15 DocCodeExamples.csproj
-rw-rw-r--  1 walcht walcht    70894 Sep 20 16:28 HSVPicker.csproj
-rw-rw-r--  1 walcht walcht    71352 Sep 20 16:28 HSVPicker.Editors.csproj
-rw-rw-r--  1 walcht walcht    48943 Sep 20 20:23 NeovimIntegrationTest.sln
-rw-rw-r--  1 walcht walcht    73162 Sep 20 16:28 PPv2URPConverters.csproj
-rw-rw-r--  1 walcht walcht    65516 Aug  8 00:15 TestNewCustomAssembly.csproj
-rw-rw-r--  1 walcht walcht    69775 Sep 20 16:28 Unity.AI.Navigation.csproj
-rw-rw-r--  1 walcht walcht    72745 Sep 20 16:28 Unity.AI.Navigation.Editor.ConversionSystem.csproj
-rw-rw-r--  1 walcht walcht    72296 Sep 20 16:28 Unity.AI.Navigation.Editor.csproj
-rw-rw-r--  1 walcht walcht    70183 Aug  8 00:15 Unity.AI.Navigation.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    66768 Aug  8 00:15 Unity.AI.Navigation.LegacyOffMeshLink.Tests.csproj
-rw-rw-r--  1 walcht walcht    66180 Aug  8 00:15 Unity.AI.Navigation.Tests.csproj
-rw-rw-r--  1 walcht walcht    65786 Aug  8 00:15 Unity.AI.Navigation.Tests.EditorInPlaymode.csproj
-rw-rw-r--  1 walcht walcht    72150 Sep 20 16:28 Unity.AI.Navigation.Updater.csproj
-rw-rw-r--  1 walcht walcht    70018 Sep 20 16:28 Unity.Burst.CodeGen.csproj
-rw-rw-r--  1 walcht walcht    70449 Sep 20 16:28 Unity.Burst.csproj
-rw-rw-r--  1 walcht walcht    72852 Sep 20 16:28 Unity.Burst.Editor.csproj
-rw-rw-r--  1 walcht walcht    71380 Sep 20 16:28 Unity.CollabProxy.Editor.csproj
-rw-rw-r--  1 walcht walcht    71514 Aug  8 00:15 Unity.Collections.BurstCompatibilityGen.csproj
-rw-rw-r--  1 walcht walcht    69199 Sep 20 16:28 Unity.Collections.CodeGen.csproj
-rw-rw-r--  1 walcht walcht    76205 Sep 20 16:28 Unity.Collections.csproj
-rw-rw-r--  1 walcht walcht    71504 Aug  8 00:15 Unity.Collections.DocCodeSamples.csproj
-rw-rw-r--  1 walcht walcht    71525 Sep 20 16:28 Unity.Collections.Editor.csproj
-rw-rw-r--  1 walcht walcht    68466 Aug  8 00:15 Unity.Collections.PerformanceTests.csproj
-rw-rw-r--  1 walcht walcht    73209 Aug  8 00:15 Unity.Collections.Tests.csproj
-rw-rw-r--  1 walcht walcht   103507 Aug  2 13:59 UnityEditor.TestRunner.csproj
-rw-rw-r--  1 walcht walcht    74616 Sep 20 16:28 UnityEditor.UI.csproj
-rw-rw-r--  1 walcht walcht    70202 Aug  8 00:15 UnityEditor.UI.EditorTests.csproj
-rw-rw-r--  1 walcht walcht    86179 Aug  2 13:59 UnityEngine.TestRunner.csproj
-rw-rw-r--  1 walcht walcht    77096 Sep 20 16:28 UnityEngine.UI.csproj
-rw-rw-r--  1 walcht walcht    73497 Aug  8 00:15 UnityEngine.UI.Tests.csproj
-rw-rw-r--  1 walcht walcht   114360 Sep 20 16:28 Unity.InputSystem.csproj
-rw-rw-r--  1 walcht walcht    68316 Sep 20 16:28 Unity.InputSystem.DocCodeSamples.csproj
-rw-rw-r--  1 walcht walcht    69585 Sep 20 16:28 Unity.InputSystem.ForUI.csproj
-rw-rw-r--  1 walcht walcht    65588 Aug  8 00:15 Unity.InputSystem.IntegrationTests.csproj
-rw-rw-r--  1 walcht walcht    66356 Aug  2 13:59 Unity.InputSystem.TestFramework.csproj
-rw-rw-r--  1 walcht walcht    68679 Aug  8 00:15 Unity.LightTransport.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    77557 Sep 20 16:28 Unity.Mathematics.csproj
-rw-rw-r--  1 walcht walcht    71714 Sep 20 16:28 Unity.Mathematics.Editor.csproj
-rw-rw-r--  1 walcht walcht    72667 Aug  8 00:15 Unity.Mathematics.Tests.csproj
-rw-rw-r--  1 walcht walcht    69756 Sep 20 16:28 Unity.Multiplayer.Center.Common.csproj
-rw-rw-r--  1 walcht walcht    76230 Sep 20 16:28 Unity.Multiplayer.Center.Editor.csproj
-rw-rw-r--  1 walcht walcht    69656 Aug  8 00:15 Unity.Multiplayer.Center.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    65575 Aug  8 00:15 Unity.Multiplayer.Center.Tests.csproj
-rw-rw-r--  1 walcht walcht    68226 Aug  8 00:15 Unity.Nuget.Mono-Cecil.csproj
-rw-rw-r--  1 walcht walcht    70545 Aug  2 13:59 Unity.PerformanceTesting.csproj
-rw-rw-r--  1 walcht walcht    70060 Aug  2 13:59 Unity.PerformanceTesting.Editor.csproj
-rw-rw-r--  1 walcht walcht   105938 Sep 20 16:28 Unity.PlasticSCM.Editor.csproj
-rw-rw-r--  1 walcht walcht    71457 Sep 20 16:28 Unity.Rendering.LightTransport.Editor.csproj
-rw-rw-r--  1 walcht walcht    78110 Sep 20 16:28 Unity.Rendering.LightTransport.Runtime.csproj
-rw-rw-r--  1 walcht walcht    95787 Sep 20 16:28 Unity.RenderPipelines.Core.Editor.csproj
-rw-rw-r--  1 walcht walcht    71450 Sep 20 16:28 Unity.RenderPipelines.Core.Editor.Shared.csproj
-rw-rw-r--  1 walcht walcht    73234 Aug  8 00:15 Unity.RenderPipelines.Core.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht   104435 Sep 20 16:28 Unity.RenderPipelines.Core.Runtime.csproj
-rw-rw-r--  1 walcht walcht    69417 Sep 20 16:28 Unity.RenderPipelines.Core.Runtime.Shared.csproj
-rw-rw-r--  1 walcht walcht    66993 Aug  8 00:15 Unity.RenderPipelines.Core.Runtime.Tests.csproj
-rw-rw-r--  1 walcht walcht    76561 Sep 20 16:28 Unity.RenderPipelines.Core.ShaderLibrary.csproj
-rw-rw-r--  1 walcht walcht    75904 Sep 20 16:28 Unity.RenderPipelines.GPUDriven.Runtime.csproj
-rw-rw-r--  1 walcht walcht    70532 Sep 20 16:28 Unity.RenderPipelines.ShaderGraph.ShaderGraphLibrary.csproj
-rw-rw-r--  1 walcht walcht    76026 Sep 20 16:28 Unity.RenderPipelines.Universal.2D.Runtime.csproj
-rw-rw-r--  1 walcht walcht    68112 Aug  8 00:15 Unity.RenderPipelines.Universal.Config.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    69529 Sep 20 16:28 Unity.RenderPipelines.Universal.Config.Runtime.csproj
-rw-rw-r--  1 walcht walcht   112112 Sep 20 16:28 Unity.RenderPipelines.Universal.Editor.csproj
-rw-rw-r--  1 walcht walcht    71044 Aug  8 00:15 Unity.RenderPipelines.Universal.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    89839 Sep 20 16:28 Unity.RenderPipelines.Universal.Runtime.csproj
-rw-rw-r--  1 walcht walcht    66283 Aug  8 00:15 Unity.RenderPipelines.Universal.Runtime.Tests.csproj
-rw-rw-r--  1 walcht walcht    84605 Sep 20 16:28 Unity.RenderPipelines.Universal.Shaders.csproj
-rw-rw-r--  1 walcht walcht    75538 Sep 20 16:28 Unity.RenderPipeline.Universal.ShaderLibrary.csproj
-rw-rw-r--  1 walcht walcht    72397 Sep 20 16:28 Unity.Rider.Editor.csproj
-rw-rw-r--  1 walcht walcht    72353 Sep 20 16:28 Unity.Searcher.Editor.csproj
-rw-rw-r--  1 walcht walcht    68284 Aug  8 00:15 Unity.Searcher.EditorTests.csproj
-rw-rw-r--  1 walcht walcht   158645 Sep 20 16:28 Unity.ShaderGraph.Editor.csproj
-rw-rw-r--  1 walcht walcht    70318 Aug  8 00:15 Unity.ShaderGraph.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    71608 Sep 20 16:28 Unity.ShaderGraph.Utilities.csproj
-rw-rw-r--  1 walcht walcht    68009 Aug  8 00:15 Unity.Sysroot.EditorTests.csproj
-rw-rw-r--  1 walcht walcht    71386 Sep 20 16:28 Unity.Sysroot.Linux_x86_64.csproj
-rw-rw-r--  1 walcht walcht    68132 Aug  8 00:15 Unity.Sysroot.Linux_x86_64.EditorTests.csproj
-rw-rw-r--  1 walcht walcht    71448 Sep 20 16:28 Unity.SysrootPackage.Editor.csproj
-rw-rw-r--  1 walcht walcht    74317 Sep 20 16:28 Unity.TextMeshPro.csproj
-rw-rw-r--  1 walcht walcht    76514 Sep 20 16:28 Unity.TextMeshPro.Editor.csproj
-rw-rw-r--  1 walcht walcht    68323 Aug  8 00:15 Unity.TextMeshPro.Editor.Tests.csproj
-rw-rw-r--  1 walcht walcht    65630 Aug  8 00:15 Unity.TextMeshPro.Tests.csproj
-rw-rw-r--  1 walcht walcht    75911 Sep 20 16:28 Unity.Timeline.csproj
-rw-rw-r--  1 walcht walcht    97763 Sep 20 16:28 Unity.Timeline.Editor.csproj
-rw-rw-r--  1 walcht walcht    71466 Sep 20 16:28 Unity.Toolchain.Linux-x86_64.csproj
-rw-rw-r--  1 walcht walcht    68069 Aug  8 00:15 Unity.Toolchain.Linux-x86_64.EditorTests.csproj
-rw-rw-r--  1 walcht walcht   125888 Sep 20 16:28 Unity.VisualScripting.Core.csproj
-rw-rw-r--  1 walcht walcht   132121 Sep 20 16:28 Unity.VisualScripting.Core.Editor.csproj
-rw-rw-r--  1 walcht walcht   121082 Sep 20 16:28 Unity.VisualScripting.Flow.csproj
-rw-rw-r--  1 walcht walcht    92883 Sep 20 16:28 Unity.VisualScripting.Flow.Editor.csproj
-rw-rw-r--  1 walcht walcht    73014 Sep 20 16:28 Unity.VisualScripting.SettingsProvider.Editor.csproj
-rw-rw-r--  1 walcht walcht    71817 Sep 20 16:28 Unity.VisualScripting.Shared.Editor.csproj
-rw-rw-r--  1 walcht walcht    72808 Sep 20 16:28 Unity.VisualScripting.State.csproj
-rw-rw-r--  1 walcht walcht    80079 Sep 20 16:28 Unity.VisualScripting.State.Editor.csproj
-rw-rw-r--  1 walcht walcht    72773 Sep 20 16:28 Unity.VisualStudio.Editor.csproj
-rw-rw-r--  1 walcht walcht    73007 Sep 20 16:28 walcht.ctvisualizer.csproj
-rw-rw-r--  1 walcht walcht    68273 Sep 20 16:28 walcht.ctvisualizer.Editor.csproj
-rw-rw-r--  1 walcht walcht    69645 Sep 20 20:23 walcht.ide.neovim.Editor.csproj
-rw-rw-r--  1 walcht walcht    70821 Sep 20 16:28 walcht.unityd3.csproj
```

I have no idea whether the Roslyn LSP's performance deteriorates proportionally (linearly)
to the number of generated`.csproj` files.

## TODOs

- [ ] automatically refresh and sync Unity project when Neovim changes/adds assets (CRUCIAL)
- [ ] add MacOS support (IMPORTANT)

## License

MIT License. Read `license.txt` file.

[com.unity.ide.visualstudio]: https://github.com/needle-mirror/com.unity.ide.visualstudio
[activate-window-by-title]: https://github.com/lucaswerkmeister/activate-window-by-title
[unity-external-tools-menu]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/images/unity-external-tools.png
[unity-asmdef]: https://docs.unity3d.com/6000.2/Documentation/Manual/cus-asmdef.html
