namespace Application.Common.Models;

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified value is null.");
    public static readonly Error NotFound = new("Error.NotFound", "The requested resource was not found.");
    public static readonly Error Failure = new("Error.Failure", "An unexpected failure occurred.");
    public static Error Custom(string code, string message) => new(code, message);
}
