# About

Neovim integration with the Unity game engine. Inspired from the official Visual
Studio editor IDE package: [com.unity.ide.visualstudio][com.unity.ide.visualstudio].

> [!Important]
> This package only supports **Unity >= 2019.4 LTS**. Support for older versions
> is not and will not be planned.

This package is constantly tested against the following Unity versions and
platforms:

| Unity                     | OS                    | Status (\*Notes)         |
| ------------------------- | --------------------- |------------------------- |
| Unity 6000.3 LTS          | Windows 10            | OK                       |
| Unity 2022.3 LTS          | Windows 10            | OK                       |
| Unity 2022.3 LTS          | Ubuntu                | OK                       |
| Unity 2020.3 LTS          | Windows 10            | OK (\*issue with the settings menu - issue with Unity's UIToolkit)   |
| Unity 2019.4 LTS          | Windows 10            | OK                       |

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

> [!Important]
> On Linux, `nvim` has to be added on PATH for non-interactive shells - that is,
> it has to appended to PATH in `~/.profile` and NOT `~/.bashrc`.

To automatically open Neovim when clicking on files/console warnings or errors,
Edit -> Preferences -> External Tools -> Set "External Script Editor" to Neovim:

<img width="498" height="98" alt="image" src="https://github.com/user-attachments/assets/f1dd73a0-3e13-412a-9cb3-7ff968d3244e" />

If you don't see `Neovim vX.XX` in the dropdown list, then the automated nvim
executable discovery has probably failed which means you should explicitly
enter your nvim executable path in (top menu) `Neovim => Settings`:

<img width="1380" height="808" alt="image" src="https://github.com/user-attachments/assets/92344e4c-2ec3-4b20-9008-0580bd9e8f30" />

As can be seen in the Neovim settings window above there a couple of settings
that you can change (also a couple of things that you are encouraged to do).

Adjust which packages to generate the .csproj files for (you will only get
LSP functionalities for those selected packages). I usually simply tick them
all (but be aware that for very large projects this might negatively affect
the LSP performance):

<img width="962" height="208" alt="image" src="https://github.com/user-attachments/assets/613a1e89-367a-4984-8ff9-1fc2df6fb976" />

You can also add a custom analyzer `.dll` through the `Browse` button. This is
usefull for adding [Microsoft.Unity.Analyzers][unity-analyzers] (which you
are encouraged to do since this removes erroneous diagnostics about unused
items):

<img width="847" height="106" alt="image" src="https://github.com/user-attachments/assets/6ee36ad1-4f2e-4a52-81ab-af3895fc4b88" />

## Change Terminal Emulator Launch Command

By default this package tries to find a default terminal emulator from a small
list of *most-common* terminals (e.g., gnome-terminal, alacritty, etc.). Of
course, if you want to supply by yourself which terminal emulator launch command
to use for launching a new Neovim server instance then you can do so via this
section in `Neovim -> Settings`:

<img width="848" height="120" alt="image" src="https://github.com/user-attachments/assets/3e10b170-e157-4637-b37e-ead95d2ffdbf" />

Where:
- `{app}` -- is replaced by the current editor path (i.e., neovim path).
- `{filePath}` -- is replaced by the path to the requested file to be opened by
  Neovim.
- `{serverSocket}` -- is replaced by the path to the IPC socket between
  the Neovim server instance and the client that will send commands. On Linux, this
  is replaced by default to `/tmp/nvimsocket`. On Windows, this is replaced by
  default to `127.0.0.1:<RANDOM-PORT>` (with <RANDOM-PORT> a randomly chosen
  available port).

> [!Note]
> Placeholders are mentioned and explained in the right pannel of `Neovim -> Settings`
> window.

On Linux, it is advised to set the window name using something like:
`--title "nvimunity"`. this is important for auto window focusing on GNOME.
This, for instance, instructs gnome-terminal to name the newly opened window as
`nvimunity`. This is crucial for focusing on the Neovim server instance
when opening a file from Unity. Change this according to your terminal emulator
but keep the new window name as `nvimunity`.

You can also optionally pass a set of environment variables as such:
`ENV_0=VALUE_0 ENV_1=VALUE_1 ... ENV_N=VALUE_N` (i.e., a space-separated list
of '=' separated environment-variable-name and value sets).

## Change Open-File Request Args

By default this package uses `--server {serverSocket} --remote-tab {filePath}`
as arguments for process execution when a request to open a file is instantiated
(i.e., replaces args here: `{app} {args}` where `{app}` is the current editor
executable path (i.e., Neovim path)). You can change this via this section
in `Neovim -> Setting` window:

<img width="849" height="181" alt="image" src="https://github.com/user-attachments/assets/b6b38397-475b-469b-b2e3-936789d9d244" />

You can also add modifier bindings so that depending on the modifier you are
applying when you open a file a different cmd is executed (e.g., when SHIFT
is pressed the file is opened in a vertical split).

## Change Jump-to-Cursor-Position Request Args

By default this package uses
`--server {serverSocket} --remote-send ":call cursor({line},{column})<CR>"` as
arguments for process execution when a request to jump to cursor position is
instantiated. You can change this via this section in `Neovim  -> Settings`
window:

<img width="848" height="80" alt="image" src="https://github.com/user-attachments/assets/56be05a5-b76b-4d7f-b802-94b8bb175b3c" />

Where:
- `{line}` -- is replaced by the line number that was requested to jump into.
- `{column}` -- is replaced by the column number that was requested to jump into.

## Change Process Timeout

You can also optionally change the process timeout (i.e., the time that this plugin
waits for a process to launch - in case it does not launch within this timeout, it
will be killed). Set this higher in case you are experiencing some issues especially
with opening-a-new-file or jumping-to-cursor-position requests:

<img width="848" height="59" alt="image" src="https://github.com/user-attachments/assets/81c8a676-e500-4060-bf81-e1677552aec8" />

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

- Issue: neovim doesn't show up in the External Script Editor on Windows when
  downloaded via a package manager.

  Solution: see this issue #18. This plugin initially looks in PATH for nvim
  (or nvim.exe on Windows) and in case it fails it looks into a set of
  *usual Neovim installation paths*. You can explicitly provide your nvim
  executable path in `Neovim -> Settings` then `ReimportAll` to refresh it.

## TODOs

- [ ] automatically refresh and sync Unity project when Neovim changes/adds assets (CRUCIAL)
- [ ] add MacOS support (IMPORTANT)

## Contributions

Given the nature of this project, contributions are more than welcome and are
very appreciated. You are encouraged to test on **Unity >= 2022.3 LTS** and not
just newer versions of Unity.

> [!Note]
> It is completely fine if you can't test on multiple platforms or/and Unity
> versions. You are encouraged to give me access rights to your PR fork so
> that I can add the potential fixes/changes for other platforms.

PRs are squashed before being merged so do not pay a lof of attention to commit
messages and commit structure.

You have to keep in a mind a couple of things when opening a PR:

  1. Only use features that are supported by **C# 7.3 or earlier**.
  1. Only use features that are supported by **.Net Standard 2.0 or earlier**.
  1. Only use features that are available on all **Unity versions 2019.1 or
  later** (Note: if it works on 2019.1 that doesn't mean that it will work on,
  for instance, 2020.3).

For heavily LLM-authored contributions, please try to reduce the verbosity (LLMs
tend to be extremely verbose and perform very large changes at once). Otherwise,
again, all contributions are welcome :-).

## License

MIT License. Read `license.txt` file.

[com.unity.ide.visualstudio]: https://github.com/needle-mirror/com.unity.ide.visualstudio
[activate-window-by-title]: https://github.com/lucaswerkmeister/activate-window-by-title
[unity-external-tools-menu]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/images/unity-external-tools.png
[unity-asmdef]: https://docs.unity3d.com/6000.2/Documentation/Manual/cus-asmdef.html
[unity-analyzers]: https://github.com/microsoft/Microsoft.Unity.Analyzers/releases
[wt]: https://github.com/microsoft/terminal
