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

![Unity's external tools menu][unity-external-tools-menu]


## TODOs

- [ ] add Windows support (CRUCIAL)
- [ ] automatically refresh and sync Unity project when Neovim changes/adds assets (CRUCIAL)
- [ ] add MacOS support (IMPORTANT)

## License

MIT License. Read `license.txt` file.

[com.unity.ide.visualstudio]: https://github.com/needle-mirror/com.unity.ide.visualstudio
[activate-window-by-title]: https://github.com/lucaswerkmeister/activate-window-by-title
[unity-external-tools-menu]: https://raw.githubusercontent.com/walcht/walcht/refs/heads/master/images/unity-external-tools.png
