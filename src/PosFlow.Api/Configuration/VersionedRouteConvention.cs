using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace PosFlow.Api.Configuration;

/// <summary>
/// Adds a versioned route (<c>api/v1/...</c>) to every controller <em>alongside</em> its existing
/// unversioned one (<c>api/...</c>), removing nothing.
///
/// A convention rather than a second <c>[Route]</c> attribute per controller: the attribute
/// approach is ten edits now plus a line every future controller has to remember, and the first one
/// that forgets silently drops out of the versioning scheme with nothing to catch it. This applies
/// to every controller including ones added later.
///
/// The unversioned routes are kept on purpose. The Angular client issues same-origin relative
/// requests to <c>/api/products</c>, <c>/api/orders</c> and so on, so replacing the routes instead
/// of adding to them would break every call. The goal is to have versioning in place before
/// external clients exist, not to migrate the current one today.
/// </summary>
public sealed class VersionedRouteConvention : IControllerModelConvention
{
    private const string VersionedPrefix = "api/v{version:apiVersion}";

    public void Apply(ControllerModel controller)
    {
        // Snapshot the existing selectors: we add to the same collection while iterating it.
        var existingSelectors = controller.Selectors.ToList();

        foreach (var selector in existingSelectors)
        {
            var template = selector.AttributeRouteModel?.Template;

            // Skips controllers with no route attribute (nothing to version) and anything already
            // versioned, so this cannot produce api/v1/api/v1/...
            if (string.IsNullOrEmpty(template) ||
                !template.StartsWith("api/", StringComparison.OrdinalIgnoreCase) ||
                template.Contains("v{version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // "api/products" -> "api/v{version:apiVersion}/products"
            var remainder = template["api/".Length..];

            controller.Selectors.Add(new SelectorModel(selector)
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = $"{VersionedPrefix}/{remainder}",
                },
            });
        }
    }
}
