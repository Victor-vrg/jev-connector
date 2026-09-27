using System;

namespace VRG.Jev;

/// <summary>
/// Internal carrier for a failed call: an invalid parameter, an error response (4xx/5xx),
/// or a request that could not be completed after retries.
/// <para>
/// This never escapes an <c>[OSAction]</c>. Each action catches it at the boundary and turns it
/// into <c>Success = false</c> plus <c>ErrorCode</c> / <c>ErrorMessage</c> / <c>StatusCode</c> on
/// the result structure, so OutSystems flows can branch on failures instead of relying on an
/// Exception Handler. The message is safe to show to an OutSystems developer: it never includes
/// the API key.
/// </para>
/// </summary>
public sealed class JevApiException : Exception
{
    /// <summary>One of the tokens in <see cref="JevErrorCodes"/>; surfaces as <c>ErrorCode</c>.</summary>
    public string ErrorCode { get; }

    /// <summary>HTTP status code returned by the API, or 0 when the call never reached it.</summary>
    public int StatusCode { get; }

    public JevApiException(string errorCode, int statusCode, string message) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public JevApiException(string errorCode, int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
