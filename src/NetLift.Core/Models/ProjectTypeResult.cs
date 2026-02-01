namespace NetLift.Core.Models;

/// <summary>
/// Represents the comprehensive result of project type detection.
/// </summary>
public class ProjectTypeResult
{
    /// <summary>
    /// Gets or sets the path to the project file.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary project type.
    /// </summary>
    public ProjectType PrimaryType { get; set; }

    /// <summary>
    /// Gets or sets the ASP.NET MVC detection result.
    /// </summary>
    public DetectionResult IsMvc { get; set; } = new();

    /// <summary>
    /// Gets or sets the ASP.NET Web API detection result.
    /// </summary>
    public DetectionResult IsWebApi { get; set; } = new();

    /// <summary>
    /// Gets or sets the ASP.NET Web Forms detection result.
    /// </summary>
    public DetectionResult IsWebForms { get; set; } = new();

    /// <summary>
    /// Gets or sets the WCF Service detection result.
    /// </summary>
    public DetectionResult IsWcfService { get; set; } = new();

    /// <summary>
    /// Gets or sets the WCF Client detection result.
    /// </summary>
    public DetectionResult IsWcfClient { get; set; } = new();

    /// <summary>
    /// Gets or sets the Entity Framework 6 usage detection result.
    /// </summary>
    public DetectionResult UsesEntityFramework6 { get; set; } = new();

    /// <summary>
    /// Gets or sets the console application detection result.
    /// </summary>
    public DetectionResult IsConsoleApp { get; set; } = new();

    /// <summary>
    /// Gets or sets the class library detection result.
    /// </summary>
    public DetectionResult IsClassLibrary { get; set; } = new();

    /// <summary>
    /// Gets or sets the WPF application detection result.
    /// </summary>
    public DetectionResult IsWpfApp { get; set; } = new();

    /// <summary>
    /// Gets or sets the Windows Forms application detection result.
    /// </summary>
    public DetectionResult IsWinFormsApp { get; set; } = new();
}
