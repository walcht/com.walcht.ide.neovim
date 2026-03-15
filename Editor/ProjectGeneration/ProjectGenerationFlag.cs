#pragma warning disable IDE0130, IDE0300, IDE0090, IDE0063, IDE0057
using System;

namespace Neovim.Editor
{
  [Flags]
  public enum ProjectGenerationFlag
  {
    None = 0,
    Embedded = 1,
    Local = 2,
    Registry = 4,
    Git = 8,
    BuiltIn = 16,
    Unknown = 32,
    PlayerAssemblies = 64,
    LocalTarBall = 128,
  }
}
