#pragma warning disable IDE0130
using System;
using System.Collections.Generic;

namespace Neovim.Editor
{
  internal class SolutionProperties
  {
    public string Name { get; set; }
    public IList<KeyValuePair<string, string>> Entries { get; set; }
    public string Type { get; set; }
  }


  internal class SolutionProjectEntry
  {
    public string ProjectFactoryGuid { get; set; }
    public string Name { get; set; }
    public string FileName { get; set; }
    public string ProjectGuid { get; set; }
    public string Metadata { get; set; }

    public bool IsSolutionFolderProjectFactory()
    {
      return ProjectFactoryGuid != null && ProjectFactoryGuid.Equals("2150E333-8FDC-42A3-9474-1A3956D46DE8", StringComparison.OrdinalIgnoreCase);
    }
  }


  internal class Solution
  {
    public SolutionProjectEntry[] Projects { get; set; }
    public SolutionProperties[] Properties { get; set; }
  }
}

