namespace Neovim.Editor 
{
  public static class TemplateCollection
  {
    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance upon opening a file (or clicking on
    ///   error message in console, etc). Depending on the modifier that is currently applied,
    ///   different commands could be sent to the Neovim server instance (e.g., open in a new
    ///   tab, or open in a vertical split, etc.). First entry is the default.
    /// </summary>
    public static readonly (string Args, string Name, string Desc)[] OpenFileArgTemplates = {
      ("--server {serverSocket} --remote-tab {filePath}",
       "Open in new tab",
       "Always opens the file in a new Neovim tab page."),
      ("--server {serverSocket} --remote-send \":drop {filePath}<CR>\"",
       "Open (reuse window)",
       "Opens in current window. If file is already open somewhere — switches to it. No new tabs."),
      ("--server {serverSocket} --remote-send \":vsplit {filePath}<CR>\"",
       "Vertical split",
       "Opens the file in a vertical split of the current window."),
      ("--server {serverSocket} --remote-send \":split {filePath}<CR>\"",
       "Horizontal split",
       "Opens the file in a horizontal split of the current window."),
    };

    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance to jump to a given cursor position.
    ///   First entry is the default.
    /// </summary>
    public static readonly (string Args, string Name, string Desc)[] JumpToCursorPositionArgTemplates = {
      ("--server {serverSocket} --remote-send \":call cursor({line},{column})<CR>\"",
       "Jump to position via cursor call",
       "Jumps to requested position in the current buffer using nvim lua cursor call."),
    };

    // NOTE: unsed?
    // // terminal launch command template - use this template for adding new launch cmds
    // public static readonly (string, string) TermLaunchCmdTemplate = ("<terminal-emulator>", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket}");

    /// <summary>
    /// List of neovim launch cmds from popular terminal emulators - this is just a hardcoded list so that non-tech-savy
    /// users can just get to using Neovim with minimal friction.
    /// </summary>
    public static readonly (string, string)[] TermLaunchCmdTemplates =
#if UNITY_EDITOR_LINUX
    {
      ("gnome-terminal", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket} " + s_NvimCmdString ),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("ptyxis", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("xterm", "-T \"nvimunity-{instanceId}\" -e {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
    };
#elif UNITY_EDITOR_OSX
    {
      ("/Applications/kitty.app/Contents/MacOS/kitty", "--title \"nvimunity-{instanceId}\" {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("/Applications/Alacritty.app/Contents/MacOS/alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("/Applications/ghostty.app/Contents/MacOS/ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket} " + s_NvimCmdString + "'"),
      ("/Applications/iTerm.app/Contents/MacOS/iTerm2", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
      ("ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket} " + s_NvimCmdString + "'"),
      ("kitty", "--title \"nvimunity-{instanceId}\" {app} {filePath} --listen {serverSocket} " + s_NvimCmdString),
    };
#else  // UNITY_EDITOR_WIN
    {
      // on Powershell, replace the ';' with "`;"
      // also be aware that Windows Terminal (WT) interprets ';' as ANYWHERE as a command to open a new tab...
      // go fucking figure why the most widely used terminal on Windows has not implemented a way to escape its symbolic
      // characters: https://github.com/microsoft/terminal/issues/13264
      ("wt", "nt {app} {filePath} --listen {serverSocket} " + s_NvimCmdString + "; nt Powershell -File {getProcessPPIDScriptPath}"),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket} " + s_NvimCmdString)
    };
#endif

    /// <summary>
    /// Command that is passed to the Neovim server instance once it is instantiated. The variables here make sense
    /// only if you are using CGNvim's Roslyn LS configuration (at https://github.com/walcht/CGNvim).
    /// In case you are not, see how CGNvim uses them and implement them in your config.
    /// If you are using WT then don't put semicolons here or it won't work. The great engineers at Microsoft decided
    /// to interpret any semicolon character ';' as a command to open a new tab and there is no way to escape it.
    /// </summary>
    private static readonly string s_NvimCmdString = string.Join("", new string[] {
      "--cmd \"",
      ":lua _G.nvim_unity_user_supplied_project_root_dir='{projectRootDir}'",
      "_G.nvim_unity_analyzer_diagnostic_scope='{analyzerDiagnosticScope}'",
      "_G.nvim_unity_compiler_diagnostic_scope='{compilerDiagnosticScope}'\"" });
  }
}
