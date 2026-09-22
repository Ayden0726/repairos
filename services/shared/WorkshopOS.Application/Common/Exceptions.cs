namespace WorkshopOS.Application.Common;

public class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode = 400) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message) : base("validation_error", message, 400) { }
}

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Invalid email or password.") : base("unauthorized", message, 401) { }
}

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "You do not have permission to do that.") : base("forbidden", message, 403) { }
}

public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string message) : base("conflict", message, 409) { }
}

public static class PasswordRules
{
    public static string? Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            return "Password must be at least 10 characters.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            return "Password must include upper, lower and a number.";
        return null;
    }
}
