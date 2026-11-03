using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ecosys.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ecosys.Tests;

/// <summary>
/// Guards every request DTO against the model-binding trap that made POST /api/workorders,
/// POST/PUT /api/clients and POST/PUT /api/assets return HTTP 500 on every single request
/// (EC-003/EC-004/EC-005).
///
/// Writing <c>[property: StringLength(300)] string Title</c> on a positional record parameter puts
/// the attribute on the generated <i>property</i> instead of the constructor parameter. MVC's
/// <c>DefaultComplexObjectValidationStrategy</c> refuses to validate such a type and throws
/// <c>InvalidOperationException: Record type '...' has validation metadata defined on property
/// '...' that will be ignored</c> — from inside the binding/validation pipeline, before any action
/// code runs, so the endpoint can never succeed regardless of payload. Worse, the limit it declares
/// is silently ignored, so it protects nothing either.
///
/// This test replicates <c>ModelMetadata.ThrowIfRecordTypeHasValidationOnProperties()</c> by
/// reflection over every <c>[FromBody]</c>-bound type in the Ecosys.Api assembly (and the complex
/// types reachable from them, which the validator also walks), so the protection covers request
/// DTOs that do not exist yet.
/// </summary>
public sealed class RequestDtoBindingTests
{
    private static readonly Assembly ApiAssembly = typeof(UpsertClientRequest).Assembly;

    [Fact]
    public void EveryFromBodyRequestType_HasValidationOnConstructorParameters_NotOnProperties()
    {
        var offenders = new List<string>();

        foreach (var type in DiscoverBodyBoundTypes())
        {
            offenders.AddRange(FindIgnoredValidationMetadata(type));
        }

        Assert.True(
            offenders.Count == 0,
            "These request records carry validation metadata on a property that is also a primary-constructor "
            + "parameter. ASP.NET Core throws during model binding, so every request to the affected endpoint "
            + "returns HTTP 500. Drop the 'property:' target so the attribute lands on the parameter "
            + $"(e.g. '[property: StringLength(200)] string Name' -> '[StringLength(200)] string Name'):{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData(typeof(CreateWorkOrderRequest))]
    [InlineData(typeof(UpsertClientRequest))]
    [InlineData(typeof(UpsertAssetRequest))]
    public void KnownRegressionRequests_AreBindable(Type requestType)
    {
        Assert.Empty(FindIgnoredValidationMetadata(requestType));
    }

    /// <summary>
    /// Positive control: proves the detector actually detects. Without this, a detector that silently
    /// matched nothing would make the sweep above pass forever.
    /// </summary>
    [Fact]
    public void Detector_FlagsARecordWithValidationOnAPrimaryConstructorProperty()
    {
        var offenders = FindIgnoredValidationMetadata(typeof(BrokenSampleRequest));

        Assert.Contains(offenders, x => x.Contains(nameof(BrokenSampleRequest.Title), StringComparison.Ordinal));
    }

    [Fact]
    public void Detector_AcceptsTheCorrectedForm()
    {
        Assert.Empty(FindIgnoredValidationMetadata(typeof(FixedSampleRequest)));
    }

    private static IReadOnlyCollection<string> FindIgnoredValidationMetadata(Type root)
    {
        var offenders = new List<string>();
        var visited = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var type = Unwrap(queue.Dequeue());
            if (type is null || !visited.Add(type))
            {
                continue;
            }

            var constructor = FindPrimaryConstructor(type);
            if (constructor is not null)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (parameter.Name is null)
                    {
                        continue;
                    }

                    var property = type.GetProperty(parameter.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (property is null || property.PropertyType != parameter.ParameterType)
                    {
                        continue;
                    }

                    var ignored = property
                        .GetCustomAttributes(inherit: true)
                        .OfType<ValidationAttribute>()
                        .Select(x => x.GetType().Name)
                        .ToList();

                    if (ignored.Count > 0)
                    {
                        offenders.Add($"  {type.FullName}.{property.Name} -> [{string.Join(", ", ignored)}]");
                    }
                }
            }

            // The validator walks nested complex members too, so a buried DTO breaks the endpoint
            // just as effectively as the top-level one.
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var nested = Unwrap(property.PropertyType);
                if (nested is not null && IsOwnedType(nested))
                {
                    queue.Enqueue(nested);
                }
            }
        }

        return offenders;
    }

    private static IEnumerable<Type> DiscoverBodyBoundTypes()
    {
        var seen = new HashSet<Type>();

        foreach (var controller in GetLoadableTypes(ApiAssembly).Where(x => typeof(ControllerBase).IsAssignableFrom(x) && !x.IsAbstract))
        {
            // [ApiController] infers the body as the source for a complex parameter that names no
            // source of its own, so an action without an explicit [FromBody] is affected just the same.
            var infersBodyBinding = controller.GetCustomAttribute<ApiControllerAttribute>(inherit: true) is not null;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in action.GetParameters())
                {
                    var bindsFromBody =
                        parameter.GetCustomAttribute<FromBodyAttribute>() is not null
                        || (infersBodyBinding
                            && parameter.GetCustomAttributes().OfType<IBindingSourceMetadata>().Any() is false);

                    if (!bindsFromBody)
                    {
                        continue;
                    }

                    var type = Unwrap(parameter.ParameterType);
                    if (type is not null && IsOwnedType(type) && seen.Add(type))
                    {
                        yield return type;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The constructor MVC would bind a record through: the single public constructor that is not the
    /// compiler-generated copy constructor. Ambiguous shapes are skipped rather than guessed at.
    /// </summary>
    private static ConstructorInfo? FindPrimaryConstructor(Type type)
    {
        if (type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is null)
        {
            // Not a record — plain classes carry validation on properties legitimately.
            return null;
        }

        var candidates = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(x =>
            {
                var parameters = x.GetParameters();
                var isCopyConstructor = parameters.Length == 1 && parameters[0].ParameterType == type;
                return !isCopyConstructor && parameters.Length > 0;
            })
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>Strips Nullable&lt;T&gt; and collection wrappers down to the element type.</summary>
    private static Type? Unwrap(Type type)
    {
        var current = Nullable.GetUnderlyingType(type) ?? type;

        if (current.IsArray)
        {
            return current.GetElementType() is { } element ? Unwrap(element) : null;
        }

        if (current.IsGenericType && typeof(IEnumerable).IsAssignableFrom(current))
        {
            var arguments = current.GetGenericArguments();
            return arguments.Length == 1 ? Unwrap(arguments[0]) : null;
        }

        return current;
    }

    /// <summary>
    /// Request DTOs live in the API surface and the shared contracts; Domain/Infrastructure types are
    /// deliberately out of scope so a DTO that happens to expose an entity does not drag the whole
    /// entity graph (and its cycles) into the walk.
    /// </summary>
    private static bool IsOwnedType(Type type) =>
        type is { IsClass: true, IsPrimitive: false }
        && type != typeof(string)
        && type.Assembly.GetName().Name is "Ecosys.Api" or "Ecosys.Shared";

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(x => x is not null)!;
        }
    }

    private sealed record BrokenSampleRequest([property: StringLength(10)] string Title);

    private sealed record FixedSampleRequest([StringLength(10)] string Title);
}
