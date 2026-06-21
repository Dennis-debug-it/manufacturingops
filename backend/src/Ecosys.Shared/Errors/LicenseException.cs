namespace Ecosys.Shared.Errors;

public sealed class LicenseException(string message, string errorCode, int statusCode = 403) : AppException(message), ILocalizedErrorCode
{
    public string ErrorCode { get; } = errorCode;
    public override int StatusCode { get; } = statusCode;
}
