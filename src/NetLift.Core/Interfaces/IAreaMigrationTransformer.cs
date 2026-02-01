using NetLift.Core.Models.Mvc;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Transforms MVC Area definitions into ASP.NET Core migration plans.
/// </summary>
public interface IAreaMigrationTransformer
{
    /// <summary>
    /// Creates a migration plan for converting an MVC Area to ASP.NET Core.
    /// </summary>
    /// <param name="area">The area definition to migrate.</param>
    /// <param name="projectRoot">The root directory of the project.</param>
    /// <param name="rootNamespace">The root namespace of the project.</param>
    /// <returns>A migration plan with folder structure, files to generate, and controllers to update.</returns>
    AreaMigrationPlan CreateMigrationPlan(AreaDefinition area, string projectRoot, string rootNamespace);

    /// <summary>
    /// Adds [Area("AreaName")] attribute to a controller source code.
    /// </summary>
    /// <param name="controllerSource">The controller source code.</param>
    /// <param name="areaName">The area name to add.</param>
    /// <returns>The modified controller source code with the Area attribute added.</returns>
    string AddAreaAttribute(string controllerSource, string areaName);
}
