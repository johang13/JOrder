using JOrder.Common.Abstractions.Results;
using Microsoft.AspNetCore.Identity;

namespace JOrder.Identity.Extensions;

public static class IdentityResultExtensions
{
    public static Error ToValidationError(this IdentityResult result, string code, string fallback)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
            throw new InvalidOperationException("ToValidationError can only be used with a failed IdentityResult.");

        return Error.Validation(code, FormatErrors(result, fallback));
    }

    private static string FormatErrors(IdentityResult result, string fallback = "An error occurred.")
    {
        var descriptions = result.Errors
            .Select(e => e.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToArray();

        return descriptions.Length == 0 ? fallback : string.Join("; ", descriptions);
    }
}