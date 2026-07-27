namespace Application.Common.Exceptions;

public class ConversionException : EarsDropException
{
    public ConversionException(string message) : base(message)
    {
    }

    public ConversionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
