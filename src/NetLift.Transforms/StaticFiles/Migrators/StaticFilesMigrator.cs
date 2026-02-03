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
        var notes = new List<string>();
        var errors = new List<string>();
        var movedFolders = new List<StaticFolder>();
        var filesMovedCount = 0;

        try
        {
            if (!dryRun)
            {
                await CreateWwwrootStructureAsync(staticFilesInfo.ProjectPath);
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
        Directory.CreateDirectory(wwwroot);
        Directory.CreateDirectory(Path.Combine(wwwroot, "css"));
        Directory.CreateDirectory(Path.Combine(wwwroot, "js"));
        Directory.CreateDirectory(Path.Combine(wwwroot, "images"));
        Directory.CreateDirectory(Path.Combine(wwwroot, "fonts"));

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task MoveFolderAsync(string projectPath, StaticFolder folder)
    {
        var sourcePath = Path.Combine(projectPath, folder.SourcePath);
        var targetPath = Path.Combine(projectPath, folder.TargetPath);

        Directory.CreateDirectory(targetPath);

        foreach (var file in folder.Files)
        {
            var sourceFile = Path.Combine(sourcePath, file);
            var targetFile = Path.Combine(targetPath, file);

            if (File.Exists(sourceFile))
            {
                var targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }

        await Task.CompletedTask;
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
