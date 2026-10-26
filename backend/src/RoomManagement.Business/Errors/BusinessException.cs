namespace RoomManagement.Business.Errors;

public sealed class BusinessException : Exception
{
    public BusinessException(BusinessErrorType type, string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Type = type;
        Code = code;
    }

    public BusinessErrorType Type { get; }
    public string Code { get; }
}
