using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JOrder.Common.Helpers;

/// <summary>
/// Provides helper methods for logging discovered ASP.NET Core controllers and their endpoints
/// at application startup.
/// </summary>
internal static class ControllerLoggingHelper
{
    /// <summary>
    /// Scans the entry assembly for all non-abstract types that inherit from
    /// <see cref="Microsoft.AspNetCore.Mvc.ControllerBase"/> and logs them in a tree structure,
    /// including each action's HTTP method and resolved route path.
    /// Route tokens such as <c>[controller]</c> are resolved to their runtime values.
    /// </summary>
    /// <param name="webApplication">
    /// The <see cref="WebApplication"/> whose logger is used for output.
    /// </param>
    internal static void LogMappedControllers(WebApplication webApplication)
    {
        LogMappedControllers(webApplication, Assembly.GetEntryAssembly());
    }

    internal static void LogMappedControllers(WebApplication webApplication, Assembly? entryAssembly)
    {
        if (entryAssembly is null)
        {
            webApplication.Logger.LogWarning("Could not determine entry assembly.");
            return;
        }

        var controllerTypes = entryAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        if (controllerTypes.Count == 0)
        {
            webApplication.Logger.LogInformation("No controllers mapped.");
            return;
        }

        var logLines = new List<string> { string.Empty, $"Mapped {controllerTypes.Count} controller(s):", string.Empty };

        for (int i = 0; i < controllerTypes.Count; i++)
        {
            var controller = controllerTypes[i];
            logLines.Add($"  {controller.Name}");

            var controllerRoute = controller.GetCustomAttribute<RouteAttribute>();
            var controllerRoutePath = controllerRoute?.Template ?? "";

            // Replace [controller] token with actual controller name (without "Controller" suffix)
            var controllerName = controller.Name.EndsWith("Controller")
                ? controller.Name.Substring(0, controller.Name.Length - "Controller".Length)
                : controller.Name;
            controllerRoutePath = controllerRoutePath.Replace("[controller]", controllerName);

            var methods = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            var methodList = methods
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name.StartsWith("Http") && a.GetType().Name.EndsWith("Attribute")))
                .OrderBy(m => m.Name)
                .ToList();

            for (int j = 0; j < methodList.Count; j++)
            {
                var method = methodList[j];
                var httpMethodAttr = method.GetCustomAttributes()
                    .First(a => a.GetType().Name.StartsWith("Http") && a.GetType().Name.EndsWith("Attribute"));

                var httpMethod = httpMethodAttr.GetType().Name.Replace("Attribute", "").Replace("Http", "");

                // Get the route template from the HttpGet/Post/etc attribute
                var routeTemplate = httpMethodAttr.GetType()
                    .GetProperty("Template")?.GetValue(httpMethodAttr) as string ?? "";

                var fullPath = string.IsNullOrEmpty(routeTemplate)
                    ? controllerRoutePath.TrimEnd('/')
                    : $"{controllerRoutePath.TrimEnd('/')}/{routeTemplate.TrimStart('/')}";

                fullPath = fullPath.TrimStart('/').TrimEnd('/');
                if (string.IsNullOrEmpty(fullPath))
                    fullPath = "/";

                var isLast = j == methodList.Count - 1;
                var prefix = isLast ? "    └─" : "    ├─";
                logLines.Add($"{prefix} {httpMethod.ToUpper()} {fullPath}");
            }

            if (i < controllerTypes.Count - 1)
                logLines.Add(string.Empty);
        }

        logLines.Add(string.Empty);
        webApplication.Logger.LogInformation(string.Join(Environment.NewLine, logLines));
    }
}
