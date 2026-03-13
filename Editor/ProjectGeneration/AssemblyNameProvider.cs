#pragma warning disable IDE0130
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;

namespace Neovim.Editor
{
  public interface IAssemblyNameProvider
  {
    string[] ProjectSupportedExtensions { get; }
    string ProjectGenerationRootNamespace { get; }
    ProjectGenerationFlag CsprojFlags { get; set; }

    string GetAssemblyNameFromScriptPath(string path);
    string GetAssemblyName(string assemblyOutputPath, string assemblyName);
    bool IsInternalizedPackagePath(string path);
    IEnumerable<Assembly> GetAssemblies(Func<string, bool> shouldFileBePartOfSolution);
    IEnumerable<string> GetAllAssetPaths();
    UnityEditor.PackageManager.PackageInfo FindForAssetPath(string assetPath);
    ResponseFileData ParseResponseFile(string responseFilePath, string projectDirectory, string[] systemReferenceDirectories);
  }

  public class AssemblyNameProvider : IAssemblyNameProvider
  {
    internal static readonly string s_AssemblyOutput = @"Temp\bin\Debug\".NormalizePathSeparators();
    internal static readonly string s_PlayerAssemblyOutput = @"Temp\bin\Debug\Player\".NormalizePathSeparators();

    private readonly Dictionary<string, UnityEditor.PackageManager.PackageInfo> m_PackageInfoCache = new();
    private ProjectGenerationFlag m_CsprojFlags = ProjectGenerationFlag.None;

    public string[] ProjectSupportedExtensions => EditorSettings.projectGenerationUserExtensions;

    public string ProjectGenerationRootNamespace => EditorSettings.projectGenerationRootNamespace;

    public ProjectGenerationFlag CsprojFlags
    {
      get { return m_CsprojFlags; }
      set { m_CsprojFlags = value; }
    }

    public AssemblyNameProvider(ProjectGenerationFlag csprojFlags)
    {
      m_CsprojFlags = csprojFlags;
    }

    public string GetAssemblyNameFromScriptPath(string path)
    {
      return CompilationPipeline.GetAssemblyNameFromScriptPath(path);
    }

    public IEnumerable<Assembly> GetAssemblies(Func<string, bool> shouldFileBePartOfSolution)
    {
      IEnumerable<Assembly> assemblies = GetAssembliesByType(AssembliesType.Editor, shouldFileBePartOfSolution, s_AssemblyOutput);

      if (!CsprojFlags.HasFlag(ProjectGenerationFlag.PlayerAssemblies))
      {
        return assemblies;
      }
      var playerAssemblies = GetAssembliesByType(AssembliesType.Player, shouldFileBePartOfSolution, s_PlayerAssemblyOutput);
      return assemblies.Concat(playerAssemblies);
    }

    private static IEnumerable<Assembly> GetAssembliesByType(AssembliesType type, Func<string, bool> shouldFileBePartOfSolution, string outputPath)
    {
      foreach (var assembly in CompilationPipeline.GetAssemblies(type))
      {
        if (assembly.sourceFiles.Any(shouldFileBePartOfSolution))
        {
          yield return new Assembly(
              assembly.name,
              outputPath,
              assembly.sourceFiles,
              assembly.defines,
              assembly.assemblyReferences,
              assembly.compiledAssemblyReferences,
              assembly.flags,
              assembly.compilerOptions
#if UNITY_2020_2_OR_NEWER
              , assembly.rootNamespace
#endif
          );
        }
      }
    }

    public string GetCompileOutputPath(string assemblyName)
    {
      // We need to keep this one for API surface check (AssemblyNameProvider is public), but not used anymore
      throw new NotImplementedException();
    }

    public IEnumerable<string> GetAllAssetPaths()
    {
      return AssetDatabase.GetAllAssetPaths();
    }

    private static string ResolvePotentialParentPackageAssetPath(string assetPath)
    {
      const string packagesPrefix = "packages/";
      if (!assetPath.StartsWith(packagesPrefix, StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      var followupSeparator = assetPath.IndexOf('/', packagesPrefix.Length);
      if (followupSeparator == -1)
      {
        return assetPath.ToLowerInvariant();
      }

      return assetPath[..followupSeparator].ToLowerInvariant();
    }

    public UnityEditor.PackageManager.PackageInfo FindForAssetPath(string assetPath)
    {
      var parentPackageAssetPath = ResolvePotentialParentPackageAssetPath(assetPath);
      if (parentPackageAssetPath == null)
      {
        return null;
      }

      if (m_PackageInfoCache.TryGetValue(parentPackageAssetPath, out var cachedPackageInfo))
      {
        return cachedPackageInfo;
      }

      var result = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(parentPackageAssetPath);
      m_PackageInfoCache[parentPackageAssetPath] = result;
      return result;
    }

    public bool IsInternalizedPackagePath(string path)
    {
      if (string.IsNullOrEmpty(path.Trim()))
      {
        return false;
      }
      var packageInfo = FindForAssetPath(path);
      if (packageInfo == null)
      {
        return false;
      }
      var packageSource = packageInfo.source;
      switch (packageSource)
      {
        case PackageSource.Embedded:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.Embedded);
        case PackageSource.Registry:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.Registry);
        case PackageSource.BuiltIn:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.BuiltIn);
        case PackageSource.Unknown:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.Unknown);
        case PackageSource.Local:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.Local);
        case PackageSource.Git:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.Git);
        case PackageSource.LocalTarball:
          return !m_CsprojFlags.HasFlag(ProjectGenerationFlag.LocalTarBall);
        default:
          break;
      }

      return false;
    }

    public ResponseFileData ParseResponseFile(string responseFilePath, string projectDirectory, string[] systemReferenceDirectories)
    {
      return CompilationPipeline.ParseResponseFile(
        responseFilePath,
        projectDirectory,
        systemReferenceDirectories
      );
    }

    internal void ResetPackageInfoCache()
    {
      m_PackageInfoCache.Clear();
    }

    public string GetAssemblyName(string assemblyOutputPath, string assemblyName)
    {
      if (assemblyOutputPath == s_PlayerAssemblyOutput)
        return assemblyName + ".Player";

      return assemblyName;
    }
  }
}
