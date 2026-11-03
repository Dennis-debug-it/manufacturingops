namespace Ecosys.Shared.Errors;

public sealed class UnauthorizedException(string message) : AppException(message)
{
    public override int StatusCode => 401;
}
