namespace NetLift.Tests.Unit.TestHelpers;

/// <summary>
/// Helper class to locate test fixture files in the tests/fixtures/ directory.
/// </summary>
public static class TestFixtureHelper
{
    /// <summary>
    /// Gets the base path to the test fixtures directory.
    /// </summary>
    /// <returns>Absolute path to the tests/fixtures directory.</returns>
    public static string GetFixturesBasePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var fixturesPath = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tests", "fixtures"));

        if (!Directory.Exists(fixturesPath))
        {
            Directory.CreateDirectory(fixturesPath);
        }

        return fixturesPath;
    }

    /// <summary>
    /// Gets the path to a specific test fixture file.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture subdirectory (e.g., "mvc5-basic").</param>
    /// <param name="fileName">Name of the file within the fixture.</param>
    /// <returns>Absolute path to the fixture file.</returns>
    public static string GetFixturePath(string fixtureName, string fileName)
    {
        var basePath = GetFixturesBasePath();
        return Path.Combine(basePath, fixtureName, fileName);
    }

    /// <summary>
    /// Gets the path to a fixture directory.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture subdirectory.</param>
    /// <returns>Absolute path to the fixture directory.</returns>
    public static string GetFixtureDirectory(string fixtureName)
    {
        var basePath = GetFixturesBasePath();
        return Path.Combine(basePath, fixtureName);
    }

    /// <summary>
    /// Checks if a fixture file exists.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture subdirectory.</param>
    /// <param name="fileName">Name of the file within the fixture.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    public static bool FixtureExists(string fixtureName, string fileName)
    {
        var path = GetFixturePath(fixtureName, fileName);
        return File.Exists(path);
    }

    /// <summary>
    /// Checks if a fixture directory exists.
    /// </summary>
    /// <param name="fixtureName">Name of the fixture subdirectory.</param>
    /// <returns>True if the directory exists, false otherwise.</returns>
    public static bool FixtureDirectoryExists(string fixtureName)
    {
        var path = GetFixtureDirectory(fixtureName);
        return Directory.Exists(path);
    }
}
