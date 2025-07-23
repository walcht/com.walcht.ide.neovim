# About

Neovim integration with the Unity game engine. Inspired from the official Visual
Studio editor IDE package: [com.unity.ide.visualstudio](https://github.com/needle-mirror/com.unity.ide.visualstudio).

## Installation

In the Unity Editor, in the top menu bar navigate to:

Window -> Package Management -> Package Manager -> navigate to plus sign on top left ->
Install package from git URL... -> paste ```https://github.com/walcht/com.walcht.ide.neovim.git```
-> install

## Usage

Make sure that **Neovim > 0.11** is installed and is globally accessible (i.e.,
added to PATH under the name "nvim")

To automatically open Neovim when clicking on files/console warnings or errors,
Edit -> Preferences -> External Tools -> Set "External Script Editor" to Neovim
-> Adjust which packages to generate the .csproj files for (you will only get
LSP functionalities for those selected packages):

![Unity's external tools menu](https://private-user-images.githubusercontent.com/89390465/469834041-8b59b404-da9d-4aba-8906-6987f235f5ca.png?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmF3LmdpdGh1YnVzZXJjb250ZW50LmNvbSIsImtleSI6ImtleTUiLCJleHAiOjE3NTMyODMyODksIm5iZiI6MTc1MzI4Mjk4OSwicGF0aCI6Ii84OTM5MDQ2NS80Njk4MzQwNDEtOGI1OWI0MDQtZGE5ZC00YWJhLTg5MDYtNjk4N2YyMzVmNWNhLnBuZz9YLUFtei1BbGdvcml0aG09QVdTNC1ITUFDLVNIQTI1NiZYLUFtei1DcmVkZW50aWFsPUFLSUFWQ09EWUxTQTUzUFFLNFpBJTJGMjAyNTA3MjMlMkZ1cy1lYXN0LTElMkZzMyUyRmF3czRfcmVxdWVzdCZYLUFtei1EYXRlPTIwMjUwNzIzVDE1MDMwOVomWC1BbXotRXhwaXJlcz0zMDAmWC1BbXotU2lnbmF0dXJlPTk2YWUxYjY3ZjBhNWVmZTIyZmJiZjZlNDViNDY4MGIxOTk3YWFhODNmZTNjNWNiOTRiZTdmYWE1ODZlN2RiMWYmWC1BbXotU2lnbmVkSGVhZGVycz1ob3N0In0.PKi7EpgvK0h1rEusb3GPj59YENbeyxsSB9s1a93-xEU)


## License

MIT License. Read `license.txt` file.
