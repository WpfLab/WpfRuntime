namespace WpfReorganize.Builder;

internal static class CleanService
{
    public static void Run(BuilderContext context)
    {
        var deletedDirs = 0;
        var skippedDirs = 0;
        var deletedFiles = 0;
        var skippedFiles = 0;

        Log.Step("Cleaning artifacts/ ...");
        if (Directory.Exists(context.ArtifactsDir))
        {
            (deletedDirs, skippedDirs) = DeleteDirectoryRecursive(context.ArtifactsDir);
            Log.Info($"  artifacts/: deleted {deletedDirs} dirs, skipped {skippedDirs} locked");
        }
        else
        {
            Log.Info("  artifacts/ does not exist, skipping");
        }

        Log.Step("Cleaning bin/ and obj/ under src/ ...");
        var srcDir = Path.Join(context.RepoRoot, "src");
        if (Directory.Exists(srcDir))
        {
            var (deleted, skipped) = CleanNamedDirectories(srcDir, ["bin", "obj"]);
            deletedDirs += deleted;
            skippedDirs += skipped;
        }

        foreach (var subDirectoryName in new[] { "Demo" })
        {
            var subDirectory = Path.Join(context.RepoRoot, subDirectoryName);
            if (Directory.Exists(subDirectory))
            {
                var (deleted, skipped) = CleanNamedDirectories(subDirectory, ["bin", "obj"]);
                deletedDirs += deleted;
                skippedDirs += skipped;
            }
        }

        Log.Step("Cleaning .vs/ ...");
        var vsDir = Path.Join(context.RepoRoot, ".vs");
        if (Directory.Exists(vsDir))
        {
            var (deleted, skipped) = DeleteDirectoryRecursive(vsDir);
            deletedDirs += deleted;
            skippedDirs += skipped;
            Log.Info($"  .vs/: deleted {deleted} dirs, skipped {skipped} locked");
        }
        else
        {
            Log.Info("  .vs/ does not exist, skipping");
        }

        Log.Step("Cleaning stray .log files in repo root ...");
        foreach (var logFile in Directory.GetFiles(context.RepoRoot, "*.log"))
        {
            try
            {
                File.Delete(logFile);
                deletedFiles++;
            }
            catch (UnauthorizedAccessException)
            {
                skippedFiles++;
            }
            catch (IOException)
            {
                skippedFiles++;
            }
        }

        Log.Info("");
        Log.Info("=== Clean summary ===");
        Log.Info($"  Directories deleted: {deletedDirs}");
        Log.Info($"  Directories skipped (locked): {skippedDirs}");
        Log.Info($"  Files deleted: {deletedFiles}");
        Log.Info($"  Files skipped (locked): {skippedFiles}");
        if (skippedDirs > 0 || skippedFiles > 0)
        {
            Log.Warn("Some files/directories were locked (likely by Visual Studio).");
            Log.Warn("Close Visual Studio and re-run 'clean' for a fully clean state.");
        }
    }

    public static void CleanArtifacts(string artifactsDir)
    {
        if (!Directory.Exists(artifactsDir))
        {
            Log.Info("artifacts does not exist, skipping cleanup");
            return;
        }

        foreach (var subDir in new[] { "bin", "obj" })
        {
            var path = Path.Join(artifactsDir, subDir);
            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                Log.Warn($"Cannot delete {subDir} (file is locked, skipping)");
            }
            catch (IOException)
            {
                Log.Warn($"Cannot delete {subDir} (file is in use, skipping)");
            }
        }

        try
        {
            foreach (var file in Directory.GetFiles(artifactsDir))
            {
                try
                {
                    File.Delete(file);
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        Log.Info("artifacts cleanup complete");
    }

    private static (int Deleted, int Skipped) DeleteDirectoryRecursive(string path)
    {
        var deleted = 0;
        var skipped = 0;

        string[] subDirectories;
        try
        {
            subDirectories = Directory.GetDirectories(path);
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warn($"  Cannot access: {path}");
            return (0, 1);
        }
        catch (IOException)
        {
            Log.Warn($"  IO error accessing: {path}");
            return (0, 1);
        }

        foreach (var subDirectory in subDirectories)
        {
            var (subDeleted, subSkipped) = DeleteDirectoryRecursive(subDirectory);
            deleted += subDeleted;
            skipped += subSkipped;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(path);
        }
        catch (UnauthorizedAccessException)
        {
            return (deleted, skipped + 1);
        }
        catch (IOException)
        {
            return (deleted, skipped + 1);
        }

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        try
        {
            Directory.Delete(path, recursive: false);
            deleted++;
        }
        catch (UnauthorizedAccessException)
        {
            skipped++;
        }
        catch (IOException)
        {
            skipped++;
        }

        return (deleted, skipped);
    }

    private static (int Deleted, int Skipped) CleanNamedDirectories(string rootDir, string[] namesToClean)
    {
        var deleted = 0;
        var skipped = 0;
        var nameSet = new HashSet<string>(namesToClean, StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (nameSet.Contains(Path.GetFileName(entry)))
                {
                    var (entryDeleted, entrySkipped) = DeleteDirectoryRecursive(entry);
                    deleted += entryDeleted;
                    skipped += entrySkipped;
                }
                else
                {
                    stack.Push(entry);
                }
            }
        }

        Log.Info($"  Deleted {deleted} directories, skipped {skipped} locked");
        return (deleted, skipped);
    }
}
