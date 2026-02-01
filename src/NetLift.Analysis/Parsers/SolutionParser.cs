using System.Text.RegularExpressions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;

namespace NetLift.Analysis.Parsers;

/// <summary>
/// Parses Visual Studio solution files to extract project and configuration information.
/// </summary>
public partial class SolutionParser : ISolutionParser
{
    private static readonly Regex ProjectLineRegex = GenerateProjectLineRegex();
    private static readonly Regex ConfigurationRegex = GenerateConfigurationRegex();
    private static readonly Regex NestedProjectRegex = GenerateNestedProjectRegex();
    private static readonly Regex FormatVersionRegex = GenerateFormatVersionRegex();
    private static readonly Regex VisualStudioVersionRegex = GenerateVisualStudioVersionRegex();

    [GeneratedRegex(@"Project\(""(?<typeGuid>\{[A-Fa-f0-9\-]+\})""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<path>[^""]+)""\s*,\s*""(?<guid>\{[A-Fa-f0-9\-]+\})""", RegexOptions.Compiled)]
    private static partial Regex GenerateProjectLineRegex();

    [GeneratedRegex(@"^\s*(?<config>[^|]+)\|(?<platform>.+?)\s*=\s*(?<config2>[^|]+)\|(?<platform2>.+?)\s*$", RegexOptions.Compiled)]
    private static partial Regex GenerateConfigurationRegex();

    [GeneratedRegex(@"^\s*(?<childGuid>\{[A-Fa-f0-9\-]+\})\s*=\s*(?<parentGuid>\{[A-Fa-f0-9\-]+\})\s*$", RegexOptions.Compiled)]
    private static partial Regex GenerateNestedProjectRegex();

    [GeneratedRegex(@"Microsoft Visual Studio Solution File, Format Version (?<version>[\d\.]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateFormatVersionRegex();

    [GeneratedRegex(@"VisualStudioVersion = (?<version>[\d\.]+)", RegexOptions.Compiled)]
    private static partial Regex GenerateVisualStudioVersionRegex();

    /// <inheritdoc/>
    public async Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}", solutionPath);
        }

        var content = await File.ReadAllTextAsync(solutionPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Solution file is empty: {solutionPath}");
        }

        var solutionInfo = new SolutionInfo
        {
            FilePath = Path.GetFullPath(solutionPath),
            Name = Path.GetFileNameWithoutExtension(solutionPath)
        };

        ParseVersions(content, solutionInfo);
        ParseProjects(content, solutionInfo);
        ParseConfigurations(content, solutionInfo);
        ParseNestedProjects(content, solutionInfo);

        return solutionInfo;
    }

    /// <inheritdoc/>
    public bool IsValidSolutionFile(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            return false;
        }

        if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var firstNonEmptyLine = File.ReadLines(solutionPath)
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            return firstNonEmptyLine != null && firstNonEmptyLine.Contains("Microsoft Visual Studio Solution File");
        }
        catch
        {
            return false;
        }
    }

    private void ParseVersions(string content, SolutionInfo solutionInfo)
    {
        var formatMatch = FormatVersionRegex.Match(content);
        if (formatMatch.Success)
        {
            solutionInfo.FormatVersion = formatMatch.Groups["version"].Value;
        }

        var vsMatch = VisualStudioVersionRegex.Match(content);
        if (vsMatch.Success)
        {
            solutionInfo.VisualStudioVersion = vsMatch.Groups["version"].Value;
        }
    }

    private void ParseProjects(string content, SolutionInfo solutionInfo)
    {
        var solutionDir = Path.GetDirectoryName(solutionInfo.FilePath) ?? string.Empty;
        var matches = ProjectLineRegex.Matches(content);

        foreach (Match match in matches)
        {
            var typeGuidStr = match.Groups["typeGuid"].Value;
            var projectGuidStr = match.Groups["guid"].Value;
            var name = match.Groups["name"].Value;
            var relativePath = match.Groups["path"].Value;

            if (!Guid.TryParse(typeGuidStr, out var typeGuid))
            {
                continue;
            }

            if (!Guid.TryParse(projectGuidStr, out var projectGuid))
            {
                continue;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
            var detectedType = ProjectTypeGuids.GetProjectType(typeGuid);

            var projectRef = new SolutionProject
            {
                ProjectGuid = projectGuid,
                TypeGuid = typeGuid,
                Name = name,
                RelativePath = relativePath,
                AbsolutePath = absolutePath,
                DetectedType = detectedType
            };

            solutionInfo.Projects.Add(projectRef);

            // Add solution folders
            if (detectedType == ProjectType.SolutionFolder)
            {
                solutionInfo.Folders.Add(new SolutionFolder
                {
                    FolderGuid = projectGuid,
                    Name = name
                });
            }
        }
    }

    private void ParseConfigurations(string content, SolutionInfo solutionInfo)
    {
        var configSection = ExtractSection(content, "GlobalSection(SolutionConfigurationPlatforms) = preSolution");
        if (string.IsNullOrEmpty(configSection))
        {
            return;
        }

        var lines = configSection.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = ConfigurationRegex.Match(line);
            if (match.Success)
            {
                var config = match.Groups["config"].Value.Trim();
                var platform = match.Groups["platform"].Value.Trim();
                var fullName = $"{config}|{platform}";

                // Avoid duplicates
                if (!solutionInfo.Configurations.Any(c => c.FullName == fullName))
                {
                    solutionInfo.Configurations.Add(new BuildConfiguration
                    {
                        Name = config,
                        Platform = platform,
                        FullName = fullName
                    });
                }
            }
        }
    }

    private void ParseNestedProjects(string content, SolutionInfo solutionInfo)
    {
        var nestedSection = ExtractSection(content, "GlobalSection(NestedProjects) = preSolution");
        if (string.IsNullOrEmpty(nestedSection))
        {
            return;
        }

        var lines = nestedSection.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = NestedProjectRegex.Match(line);
            if (match.Success)
            {
                var childGuidStr = match.Groups["childGuid"].Value;
                var parentGuidStr = match.Groups["parentGuid"].Value;

                if (Guid.TryParse(childGuidStr, out var childGuid) &&
                    Guid.TryParse(parentGuidStr, out var parentGuid))
                {
                    var parentFolder = solutionInfo.Folders.FirstOrDefault(f => f.FolderGuid == parentGuid);
                    if (parentFolder != null)
                    {
                        parentFolder.ProjectGuids.Add(childGuid);
                    }
                }
            }
        }
    }

    private string ExtractSection(string content, string sectionHeader)
    {
        var startIndex = content.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (startIndex == -1)
        {
            return string.Empty;
        }

        var endMarker = "EndGlobalSection";
        var endIndex = content.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        if (endIndex == -1)
        {
            return string.Empty;
        }

        return content.Substring(startIndex + sectionHeader.Length, endIndex - startIndex - sectionHeader.Length);
    }
}
