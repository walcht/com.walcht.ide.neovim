# About

Neovim integration with the Unity game engine. Inspired from the official Visual
Studio editor IDE package: [com.unity.ide.visualstudio][com.unity.ide.visualstudio].

> [!Important]
> This package only supports **Unity >= 2022.3 LTS**. Support for older versions
> is not and will not be planned.

## Features

- Cross-platform support (Linux and Windows 10/11 - MacOS is TODO)
- `.csproj` generation for LSP purposes
- Opening of a new-tab in the currently running Neovim server instance
- Jumping to cursor position on the requested file in the currently running Neovim
  server instance
- Auto focusing on Neovim server instance window (on Linux, currently only on GNOME
  and full support on Windows with Windows Terminal)
- Fully customizable commands (terminal launch command, open-file arguments, and
  jump-to-cursor position arguments)
- Option to add custom analyzers to generated `.csproj` files (usefull for
  `Microsoft.Unity.Analyzers`)
- Persistent Neovim session (i.e., when you close the editor while a Neovim server
  instance is running and then you open the same project again, the same instance
  is used - persistency is achieved through:
  ```EditorPrefs.SetString("NvimUnityConfigJson", configJson)```)

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

<img width="961" height="336" alt="image" src="https://github.com/user-attachments/assets/8bcd00c4-923e-49fe-9823-37bce77e50f1" />

You can also add a custom analyzer `.dll` through the `Browse` button. This is
usefull for adding [Microsoft.Unity.Analyzers][unity-analyzers].

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
-rw-rw-r--  1 walcht walcht    48943 Sep 20 20:23 NeovimIntegrationTest.sln
-rw-rw-r--  1 walcht walcht    73162 Sep 20 16:28 PPv2URPConverters.csproj

...

-rw-rw-r--  1 walcht walcht    72773 Sep 20 16:28 Unity.VisualStudio.Editor.csproj
-rw-rw-r--  1 walcht walcht    73007 Sep 20 16:28 walcht.ctvisualizer.csproj
-rw-rw-r--  1 walcht walcht    68273 Sep 20 16:28 walcht.ctvisualizer.Editor.csproj
-rw-rw-r--  1 walcht walcht    69645 Sep 20 20:23 walcht.ide.neovim.Editor.csproj
-rw-rw-r--  1 walcht walcht    70821 Sep 20 16:28 walcht.unityd3.csproj
```

I have no idea whether the Roslyn LSP's performance deteriorates proportionally (linearly)
to the number of generated`.csproj` files.

## Auto Window Focusing on Windows

> [!Note]
> It is recommended that you use [Windows Terminal][wt] (`wt`) on Windows 10/11
> and configure its default shell to Powershell.

Currently, auto window focusing is only tested on:

 - `wt` (Windows Terminal)
 - and `alacritty`

The way Neovim server window focusing is achieved depends on whether your
terminal launch command does NOT spawn a child process that is responsible for
the Neovim server window (e.g., `alacritty`). This case is very simple to handle
by just getting the launched process' handle and using Win32 API to focus on
said handle.

If, on the other hand, your terminal launch command spawns a child process and
exits immediately (e.g., `wt`) then figuring out the handle of the window owning
the Neovim server instance is much trickier. To do so, a Powershell script that
is executed in the child process (or any other child process as long as the
parent IS the process containing the Neovim server instance) sends its parent
PID (PPID) to a pipe and this plugin reads it and gets the window handle from
it.

It is therefore important to note, again, that depending on the terminal launch
command you set up, auto window focusing may or may not work. Since there are a
lot of terminals out there, I cannot dedicate enough time to support all of
them - please do open a PR in case you think your custom terminal launch
command should be added/supported.

## Known Issues

- Issue: initial file opening successfully opens a new Neovim server instance but
  subsequent file openings do not open a new tab.
  
  Solution: this is probably due to a low process timeout - go to the top menu,
  `Neovim -> Change Process Timeout` and set it to something high (like 400ms).
  If this solves the issue - then set it to something lower so that you do not
  have to wait (2x400ms) for the Neovim server instance to open a new tab. This
  is a limitation that is hard to circumvent because each hardware/OS may
  execute cmd shell processes in different times.

## TODOs

- [ ] automatically refresh and sync Unity project when Neovim changes/adds assets (CRUCIAL)
- [ ] add MacOS support (IMPORTANT)

## License

MIT License. Read `license.txt` file.

[com.unity.ide.visualstudio]: https://github.com/needle-mirror/com.unity.ide.visualstudio
[activate-window-by-title]: https://github.com/lucaswerkmeister/activate-window-by-title
[unity-external-tools-menu]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/images/unity-external-tools.png
[unity-asmdef]: https://docs.unity3d.com/6000.2/Documentation/Manual/cus-asmdef.html
[unity-analyzers]: https://github.com/microsoft/Microsoft.Unity.Analyzers/releases
[wt]: https://github.com/microsoft/terminal
