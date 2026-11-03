namespace Ecosys.Shared.Errors;

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;
}
