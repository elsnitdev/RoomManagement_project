namespace RoomManagement.Business.Errors;

public sealed class BusinessException : Exception
{
    public BusinessException(BusinessErrorType type, string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Type = type;
        Code = code;
    }

    public BusinessErrorType Type { get; }
    public string Code { get; }
}
