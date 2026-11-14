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

    public static BusinessException BadRequest(string message, string code = "BAD_REQUEST") =>
        new(BusinessErrorType.Validation, code, message);

    public static BusinessException NotFound(string message, string code = "NOT_FOUND") =>
        new(BusinessErrorType.NotFound, code, message);

    public static BusinessException Forbidden(string message, string code = "FORBIDDEN") =>
        new(BusinessErrorType.Forbidden, code, message);

    public static BusinessException Conflict(string message, string code = "CONFLICT") =>
        new(BusinessErrorType.Conflict, code, message);

    public static BusinessException Unprocessable(string message, string code = "INVALID_OPERATION") =>
        new(BusinessErrorType.InvalidOperation, code, message);
}
