using System.Reflection;
using System.Text.RegularExpressions;
using Ecosys.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Ecosys.Tests;

/// <summary>Two actions answering the same verb and path make every request to it fail with a 500.</summary>
public sealed class RouteUniquenessTests
{
    [Fact]
    public void NoTwoActionsShareAVerbAndRoute()
    {
        var routes = new List<(string Key, string Action)>();
        foreach (var controller in typeof(AssetsController).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var prefixes = controller.GetCustomAttributes<RouteAttribute>(true).Select(x => x.Template).DefaultIfEmpty("").ToList();
            var controllerName = controller.Name.EndsWith("Controller") ? controller.Name[..^"Controller".Length] : controller.Name;
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>(true))
                {
                    foreach (var prefix in prefixes)
                    {
                        var template = http.Template ?? "";
                        var full = template.StartsWith('/') || template.StartsWith("~/")
                            ? template.TrimStart('~')
                            : $"/{prefix.Trim('/')}/{template}";
                        full = full.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
                        // Parameter names don't matter for matching; constraints do.
                        full = Regex.Replace(full, @"\{[^:}]+(:[^}]+)?\}", m => "{" + m.Groups[1].Value + "}").TrimEnd('/').ToLowerInvariant();
                        full = Regex.Replace(full, "/+", "/");
                        foreach (var verb in http.HttpMethods)
                            routes.Add(($"{verb} {full}", $"{controller.Name}.{method.Name}"));
                    }
                }
            }
        }

        var clashes = routes.GroupBy(x => x.Key).Where(g => g.Select(x => x.Action).Distinct().Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Action).Distinct())}").ToList();
        Assert.True(clashes.Count == 0, "Ambiguous routes:\n" + string.Join("\n", clashes));
    }
}
