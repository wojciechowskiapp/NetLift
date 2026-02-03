using System.Text.RegularExpressions;
using NetLift.Core.Interfaces.StaticFiles;
using NetLift.Core.Models.StaticFiles;

namespace NetLift.Transforms.StaticFiles.Migrators;

/// <summary>
/// Migrates static files to the wwwroot folder structure.
/// </summary>
public class StaticFilesMigrator : IStaticFilesMigrator
{
    /// <inheritdoc />
    public async Task<StaticFilesMigrationResult> MigrateAsync(StaticFilesInfo staticFilesInfo, bool dryRun = false)
    {
        // Console.Error.WriteLine($"[DEBUG] StaticFilesMigrator.MigrateAsync starting, dryRun={dryRun}");
        var notes = new List<string>();
        var errors = new List<string>();
        var movedFolders = new List<StaticFolder>();
        var filesMovedCount = 0;

        try
        {
            if (!dryRun)
            {
                // Console.Error.WriteLine("[DEBUG] Calling CreateWwwrootStructureAsync...");
                await CreateWwwrootStructureAsync(staticFilesInfo.ProjectPath);
                // Console.Error.WriteLine("[DEBUG] CreateWwwrootStructureAsync completed");
            }

            foreach (var folder in staticFilesInfo.Folders)
            {
                try
                {
                    if (!dryRun)
                    {
                        await MoveFolderAsync(staticFilesInfo.ProjectPath, folder);
                    }
                    movedFolders.Add(folder);
                    filesMovedCount += folder.Files.Count;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to move folder {folder.SourcePath}: {ex.Message}");
                }
            }

            var referencesUpdated = await UpdateReferencesAsync(
                staticFilesInfo.ProjectPath,
                staticFilesInfo.References,
                dryRun);

            // Fix CSS url() paths in moved CSS files
            var cssUrlsFixed = await FixCssUrlPathsAsync(staticFilesInfo.ProjectPath, dryRun);
            if (cssUrlsFixed > 0)
            {
                notes.Add($"Fixed {cssUrlsFixed} CSS url() path references");
            }

            // Handle root-level files (favicon.ico, robots.txt)
            var rootFilesHandled = await HandleRootFilesAsync(staticFilesInfo.ProjectPath, dryRun);
            if (rootFilesHandled > 0)
            {
                notes.Add($"Moved {rootFilesHandled} root files to wwwroot");
            }

            if (staticFilesInfo.HasBundleConfig)
            {
                notes.Add("BundleConfig.cs detected - review and remove after migration");
            }

            return new StaticFilesMigrationResult
            {
                Success = errors.Count == 0,
                MovedFolders = movedFolders,
                FilesMovedCount = filesMovedCount,
                ReferencesUpdatedCount = referencesUpdated,
                Confidence = CalculateConfidence(staticFilesInfo, errors.Count),
                Notes = notes,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            return new StaticFilesMigrationResult
            {
                Success = false,
                MovedFolders = [],
                FilesMovedCount = 0,
                ReferencesUpdatedCount = 0,
                Confidence = 0,
                Notes = notes,
                Errors = [$"Migration failed: {ex.Message}"]
            };
        }
    }

    /// <inheritdoc />
    public async Task CreateWwwrootStructureAsync(string projectPath)
    {
        var wwwroot = Path.Combine(projectPath, "wwwroot");

        // Create directories with retry for Windows file system transient errors
        await CreateDirectoryWithRetryAsync(wwwroot);

        // Small delay to let Windows/antivirus settle
        await Task.Delay(50);

        await CreateDirectoryWithRetryAsync(Path.Combine(wwwroot, "css"));
        await CreateDirectoryWithRetryAsync(Path.Combine(wwwroot, "js"));
        await CreateDirectoryWithRetryAsync(Path.Combine(wwwroot, "images"));
        await CreateDirectoryWithRetryAsync(Path.Combine(wwwroot, "fonts"));

        // Another small delay after structure creation
        await Task.Delay(50);
    }

    /// <summary>
    /// Creates a directory with retry logic to handle transient Windows file system errors.
    /// </summary>
    private static async Task CreateDirectoryWithRetryAsync(string path, int maxRetries = 5)
    {
        // If directory already exists, nothing to do
        if (Directory.Exists(path))
        {
            return;
        }

        Exception? lastException = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Ensure parent directory exists first
                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                // Check again in case another thread created it
                if (Directory.Exists(path))
                {
                    return;
                }

                Directory.CreateDirectory(path);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
                if (attempt < maxRetries - 1)
                {
                    // Wait and retry - Windows may still have a handle on the path
                    await Task.Delay(200 * (attempt + 1));
                }
            }
            catch (IOException ex)
            {
                lastException = ex;
                if (attempt < maxRetries - 1)
                {
                    // Wait and retry - file system may be busy
                    await Task.Delay(200 * (attempt + 1));
                }
            }
        }

        // Check one more time if directory exists
        if (Directory.Exists(path))
        {
            return;
        }

        // Throw the last exception with more context
        throw new IOException($"Failed to create directory '{path}' after {maxRetries} attempts. Last error: {lastException?.Message}", lastException);
    }

    /// <inheritdoc />
    public async Task MoveFolderAsync(string projectPath, StaticFolder folder)
    {
        var sourcePath = Path.Combine(projectPath, folder.SourcePath);
        var targetPath = Path.Combine(projectPath, folder.TargetPath);

        await CreateDirectoryWithRetryAsync(targetPath);

        foreach (var file in folder.Files)
        {
            var sourceFile = Path.Combine(sourcePath, file);
            var targetFile = Path.Combine(targetPath, file);

            if (File.Exists(sourceFile))
            {
                var targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    await CreateDirectoryWithRetryAsync(targetDir);
                }

                await CopyFileWithRetryAsync(sourceFile, targetFile);
            }
        }
    }

    /// <summary>
    /// Copies a file with retry logic to handle transient Windows file system errors.
    /// </summary>
    private static async Task CopyFileWithRetryAsync(string source, string target, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(100 * (attempt + 1));
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(100 * (attempt + 1));
            }
        }

        // Final attempt without catching
        File.Copy(source, target, overwrite: true);
    }

    /// <summary>
    /// Gets files with retry logic to handle transient Windows file system errors (e.g., antivirus scanning).
    /// </summary>
    private static async Task<string[]> GetFilesWithRetryAsync(string path, string searchPattern, int maxRetries = 5)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(200 * (attempt + 1));
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(200 * (attempt + 1));
            }
        }

        // Final attempt without catching
        return Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
    }

    /// <inheritdoc />
    public async Task<int> UpdateReferencesAsync(string projectPath, IReadOnlyList<StaticFileReference> references, bool dryRun = false)
    {
        var updatedCount = 0;
        var fileUpdates = references
            .GroupBy(r => r.SourceFile)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (filePath, fileRefs) in fileUpdates)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var originalContent = content;

                foreach (var reference in fileRefs)
                {
                    if (reference.OriginalPath != reference.NewPath)
                    {
                        content = content.Replace(reference.OriginalPath, reference.NewPath);
                        updatedCount++;
                    }
                }

                if (!dryRun && content != originalContent)
                {
                    await File.WriteAllTextAsync(filePath, content);
                }
            }
            catch (IOException)
            {
                // Skip files that can't be read/written
            }
        }

        return updatedCount;
    }

    /// <inheritdoc />
    public string GenerateStaticFilesMiddleware()
    {
        return @"// Add static files middleware in Program.cs
app.UseStaticFiles();

// If you need to serve files from additional locations:
// app.UseStaticFiles(new StaticFileOptions
// {
//     FileProvider = new PhysicalFileProvider(
//         Path.Combine(builder.Environment.ContentRootPath, ""MyStaticFiles"")),
//     RequestPath = ""/static""
// });";
    }

    /// <summary>
    /// Fixes CSS url() paths in moved CSS files to reflect new wwwroot structure.
    /// </summary>
    private async Task<int> FixCssUrlPathsAsync(string projectPath, bool dryRun)
    {
        // Console.Error.WriteLine("[DEBUG] FixCssUrlPathsAsync starting...");
        var wwwrootCss = Path.Combine(projectPath, "wwwroot", "css");
        // Console.Error.WriteLine($"[DEBUG] Checking if {wwwrootCss} exists...");
        if (!Directory.Exists(wwwrootCss))
        {
            // Console.Error.WriteLine("[DEBUG] wwwroot/css does not exist, returning 0");
            return 0;
        }

        var fixedCount = 0;
        // Console.Error.WriteLine("[DEBUG] Calling GetFilesWithRetryAsync...");
        var cssFiles = await GetFilesWithRetryAsync(wwwrootCss, "*.css");
        // Console.Error.WriteLine($"[DEBUG] GetFilesWithRetryAsync returned {cssFiles.Length} files");

        foreach (var cssFile in cssFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(cssFile);
                var originalContent = content;

                // Fix relative paths from old Content folder structure to new wwwroot structure
                // url('../fonts/...') → url('../fonts/...') (fonts stays same level)
                // url('../images/...') → url('../images/...')
                // url('fonts/...') → url('../fonts/...')  (if CSS was in Content/css subfolder)

                // Pattern 1: url pointing to ../fonts or ../images (already correct relative path)
                // Pattern 2: url pointing to fonts/ or images/ without parent - needs adjustment
                content = Regex.Replace(content,
                    @"url\s*\(\s*[""']?(?:\.\./)?(fonts|images)/([^""')]+)[""']?\s*\)",
                    match =>
                    {
                        var folder = match.Groups[1].Value;
                        var path = match.Groups[2].Value;
                        return $"url('../{folder}/{path}')";
                    },
                    RegexOptions.IgnoreCase);

                // Fix Content/ references to proper wwwroot paths
                content = Regex.Replace(content,
                    @"url\s*\(\s*[""']?(?:~/)?Content/([^""')]+)[""']?\s*\)",
                    match =>
                    {
                        var path = match.Groups[1].Value;
                        // Content/images → ../images, Content/fonts → ../fonts
                        if (path.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"url('../{path}')";
                        }
                        if (path.StartsWith("fonts/", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"url('../{path}')";
                        }
                        // Other content files stay in css folder
                        return $"url('{path}')";
                    },
                    RegexOptions.IgnoreCase);

                if (content != originalContent)
                {
                    fixedCount++;
                    if (!dryRun)
                    {
                        await File.WriteAllTextAsync(cssFile, content);
                    }
                }
            }
            catch (IOException)
            {
                // Skip files that can't be processed
            }
        }

        return fixedCount;
    }

    /// <summary>
    /// Handles root-level files like favicon.ico and robots.txt.
    /// </summary>
    private async Task<int> HandleRootFilesAsync(string projectPath, bool dryRun)
    {
        var rootFiles = new[] { "favicon.ico", "robots.txt", "sitemap.xml", "browserconfig.xml" };
        var wwwroot = Path.Combine(projectPath, "wwwroot");
        var movedCount = 0;

        foreach (var fileName in rootFiles)
        {
            var sourcePath = Path.Combine(projectPath, fileName);
            var targetPath = Path.Combine(wwwroot, fileName);

            if (File.Exists(sourcePath) && !File.Exists(targetPath))
            {
                if (!dryRun)
                {
                    File.Copy(sourcePath, targetPath, overwrite: false);
                }
                movedCount++;
            }
        }

        await Task.CompletedTask;
        return movedCount;
    }

    private static int CalculateConfidence(StaticFilesInfo info, int errorCount)
    {
        var confidence = 95;

        if (errorCount > 0)
        {
            confidence -= errorCount * 10;
        }

        if (info.HasBundleConfig)
        {
            confidence -= 10; // Needs manual bundle review
        }

        return Math.Max(confidence, 0);
    }
}
