namespace NetLift.Core.Models;

/// <summary>
/// Common project type GUIDs used in Visual Studio solution files.
/// </summary>
public static class ProjectTypeGuids
{
    /// <summary>
    /// C# project type GUID.
    /// </summary>
    public static readonly Guid CSharp = new("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");

    /// <summary>
    /// Solution folder type GUID.
    /// </summary>
    public static readonly Guid SolutionFolder = new("2150E333-8FDC-42A3-9474-1A3956D46DE8");

    /// <summary>
    /// ASP.NET Web Application project type GUID.
    /// </summary>
    public static readonly Guid Web = new("349C5851-65DF-11DA-9384-00065B846F21");

    /// <summary>
    /// WCF service application project type GUID.
    /// </summary>
    public static readonly Guid Wcf = new("3D9AD99F-2412-4246-B90B-4EAA41C64699");

    /// <summary>
    /// WPF application project type GUID.
    /// </summary>
    public static readonly Guid Wpf = new("60DC8134-EBA5-43B8-BCC9-BB4BC16C2548");

    /// <summary>
    /// Windows Forms application project type GUID.
    /// </summary>
    public static readonly Guid WinForms = new("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");

    /// <summary>
    /// Test project type GUID.
    /// </summary>
    public static readonly Guid Test = new("3AC096D0-A1C2-E12C-1390-A8335801FDAB");

    /// <summary>
    /// ASP.NET MVC project type GUID.
    /// </summary>
    public static readonly Guid Mvc = new("E3E379DF-F4C6-4180-9B81-6769533ABE47");

    /// <summary>
    /// Determines the project type based on the type GUID.
    /// </summary>
    /// <param name="typeGuid">The project type GUID.</param>
    /// <returns>The identified project type.</returns>
    public static ProjectType GetProjectType(Guid typeGuid)
    {
        if (typeGuid == SolutionFolder)
            return ProjectType.SolutionFolder;
        if (typeGuid == Web)
            return ProjectType.CSharpWeb;
        if (typeGuid == Wcf)
            return ProjectType.CSharpWcf;
        if (typeGuid == Wpf)
            return ProjectType.CSharpWpf;
        if (typeGuid == Test)
            return ProjectType.CSharpTest;
        if (typeGuid == Mvc)
            return ProjectType.CSharpMvc;
        if (typeGuid == CSharp)
            return ProjectType.CSharpClassLibrary;

        return ProjectType.Unknown;
    }
}
