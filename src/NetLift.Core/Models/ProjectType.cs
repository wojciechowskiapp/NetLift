namespace NetLift.Core.Models;

/// <summary>
/// Represents the type of a Visual Studio project.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Unknown or unrecognized project type.
    /// </summary>
    Unknown,

    /// <summary>
    /// C# class library project.
    /// </summary>
    CSharpClassLibrary,

    /// <summary>
    /// C# console application project.
    /// </summary>
    CSharpConsole,

    /// <summary>
    /// ASP.NET web application project.
    /// </summary>
    CSharpWeb,

    /// <summary>
    /// WCF service application project.
    /// </summary>
    CSharpWcf,

    /// <summary>
    /// WPF application project.
    /// </summary>
    CSharpWpf,

    /// <summary>
    /// Windows Forms application project.
    /// </summary>
    CSharpWinForms,

    /// <summary>
    /// Solution folder (virtual folder in solution).
    /// </summary>
    SolutionFolder,

    /// <summary>
    /// Test project.
    /// </summary>
    CSharpTest,

    /// <summary>
    /// ASP.NET MVC project.
    /// </summary>
    CSharpMvc,

    /// <summary>
    /// ASP.NET Web API project.
    /// </summary>
    AspNetWebApi,

    /// <summary>
    /// ASP.NET Web Forms project.
    /// </summary>
    AspNetWebForms,

    /// <summary>
    /// WCF service project.
    /// </summary>
    WcfService,

    /// <summary>
    /// WCF client project.
    /// </summary>
    WcfClient,

    /// <summary>
    /// ASP.NET Core MVC/API project (SDK-style with Microsoft.NET.Sdk.Web).
    /// </summary>
    AspNetCore
}
