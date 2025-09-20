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

On Linux, this package tries to find a default terminal emulator from a small
list of *most-common* terminals (e.g., gnome-terminal, alacritty, etc.). Of
course, if you want to supply by yourself which terminal emulator launch command
to use for launching a new Neovim server instance then you can do so via the
top menu option in the Unity Editor: `Neovim -> Change Terminal Launch Cmd` which
will show the following popup:

<img width="609" height="176" alt="image" src="https://github.com/user-attachments/assets/469f5bc6-b4a8-43f0-8885-d49c914935d6" />

- `{app}` -- will be replaced by your neovim path. Please keep this as it is in
  your launch command.
- `{filePath}` -- will be replaced by the path to the file that will be opened
  by Neovim. Please keep this as it is in your launch command.
- `{serverSocketPath}` -- will be replaced by the path to the IPC socket between
  the Neovim server instance and the client that will send commands. On Linux, this
  is repalce by default to `/tmp/nvimsocket`. Please keep this as it is in your
  launch command.
- `--title "nvimunity"` -- this instructs gnome-terminal to name the newly opened
  window as `nvimunity`. This is crucial for focusing on the Neovim server instance
  when opening a file from Unity. Change this according to your terminal emulator
  but keep the new window name as `nvimunity`.

## TODOs

- [ ] add Windows support (CRUCIAL)
- [ ] automatically refresh and sync Unity project when Neovim changes/adds assets (CRUCIAL)
- [ ] add MacOS support (IMPORTANT)

## License

MIT License. Read `license.txt` file.

[com.unity.ide.visualstudio]: https://github.com/needle-mirror/com.unity.ide.visualstudio
[activate-window-by-title]: https://github.com/lucaswerkmeister/activate-window-by-title
[unity-external-tools-menu]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/images/unity-external-tools.png
