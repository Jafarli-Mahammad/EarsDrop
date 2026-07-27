namespace Application.Common.Exceptions;

public class MetadataException : EarsDropException
{
    public MetadataException(string message) : base(message)
    {
    }

    public MetadataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
