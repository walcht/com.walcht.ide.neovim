#pragma warning disable IDE0130, IDE0300, IDE0090, IDE0063, IDE0057
namespace Neovim.Editor
{
  public interface IGUIDGenerator
  {
    string ProjectGuid(string projectName, string assemblyName);
    string SolutionGuid(string projectName, ScriptingLanguage scriptingLanguage);
  }

  class GUIDProvider : IGUIDGenerator
  {
    public string ProjectGuid(string projectName, string assemblyName)
    {
      return SolutionGuidGenerator.GuidForProject(projectName + assemblyName);
    }

    public string SolutionGuid(string projectName, ScriptingLanguage scriptingLanguage)
    {
      return SolutionGuidGenerator.GuidForSolution(projectName, scriptingLanguage);
    }
  }
}
