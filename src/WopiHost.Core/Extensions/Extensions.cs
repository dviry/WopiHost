using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Extensions;

internal static class Extensions
{
    /// <summary>
    /// Copies the stream to a byte array.
    /// </summary>
    /// <param name="input">Stream to read from</param>
    /// <returns>Byte array copy of a stream</returns>
    public static async Task<byte[]> ReadBytesAsync(this Stream input)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Tries to parse integer from string. Returns null if parsing fails.
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <returns>Integer parsed from <paramref name="s"/></returns>
    public static int? ToNullableInt(this string s)
    {
        if (int.TryParse(s, out var i)) return i;
        return null;
    }

    /// <summary>
    /// Converts <see cref="DateTime"/> to UNIX timestamp.
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        DateTimeOffset dto = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Replaces forbidden characters in identity properties with an underscore.
    /// Accordingly to: https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-response#requirements-for-user-identity-properties
    /// </summary>
    /// <param name="identity">Identity property value</param>
    /// <returns>String safe to use as an identity property</returns>
    public static string ToSafeIdentity(this string identity)
    {
        const string forbiddenChars = "<>\"#{}^[]`\\/";
        return forbiddenChars.Aggregate(identity, (current, forbiddenChar) => current.Replace(forbiddenChar, '_'));
    }

    /// <summary>
    /// Get WOPI authentication token
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    public static string? GetAccessToken(this HttpContext httpContext)
    {
        //TODO: an alternative would be HttpContext.GetTokenAsync(AccessTokenDefaults.AuthenticationScheme, AccessTokenDefaults.AccessTokenQueryName).Result (if the code below doesn't work)
        var authenticateInfo = httpContext.AuthenticateAsync(AccessTokenDefaults.AUTHENTICATION_SCHEME).Result;
        return authenticateInfo?.Properties?.GetTokenValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME);
    }

    /// <summary>
    /// Creates an absolute URL to access a WOPI object of choice.
    /// </summary>
    /// <param name="url">url helper</param>
    /// <param name="routeName">Name of the route to be called from <see cref="WopiRouteNames"/>.</param>
    /// <param name="identifier">Identifier of an object associated to the controller.</param>
    /// <param name="accessToken">Access token to use for authentication for the given controller.</param>
    /// <returns>url to requested route with template parameters</returns>
    public static string GetWopiUrl(this IUrlHelper url, string routeName, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);

        accessToken ??= url.ActionContext.HttpContext.GetAccessToken();
        var protocol = url.ActionContext.HttpContext.Request.Scheme;
        return url.RouteUrl(routeName, new { id = identifier ?? string.Empty, access_token = accessToken }, protocol)
            ?? throw new InvalidOperationException(routeName + " route not found");
    }

    /// <summary>
    /// Creates an absolute URL to access a WOPI resource.
    /// </summary>
    /// <param name="url">url helper</param>
    /// <param name="resourceType">which Wopi resource to access</param>
    /// <param name="identifier">resource unique identifier</param>
    /// <param name="accessToken">Access token to use for authentication for the given controller.</param>
    public static string GetWopiUrl(this IUrlHelper url, WopiResourceType resourceType, string? identifier = null, string? accessToken = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.GetWopiUrl(
            resourceType switch
            {
                WopiResourceType.File => WopiRouteNames.CheckFileInfo,
                WopiResourceType.Container => WopiRouteNames.CheckContainerInfo,
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
            }, identifier, accessToken);
    }

    /// <summary>
    /// Checks if the resource has a specific permission as setup by <see cref="WopiAuthorizationHandler"/>
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    public static bool IsPermitted(this HttpContext httpContext, Permission permission)
    {
        return httpContext.Items.TryGetValue(permission, out var value) && value is bool boolValue && boolValue;
    }
}