namespace Ecosys.Shared.Errors;

public sealed class BusinessRuleException(string message) : AppException(message)
{
    public override int StatusCode => 400;
}
