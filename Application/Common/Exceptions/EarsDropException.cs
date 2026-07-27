namespace Application.Common.Exceptions;

public class EarsDropException : Exception
{
    public EarsDropException(string message) : base(message)
    {
    }

    public EarsDropException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
