namespace Application.Common.Exceptions;

public class DownloadException : EarsDropException
{
    public DownloadException(string message) : base(message)
    {
    }

    public DownloadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
