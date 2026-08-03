using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling request: {RequestName}", requestName);

        try
        {
            var response = await next();
            _logger.LogInformation("Successfully handled request: {RequestName}", requestName);
            return response;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected control-flow signal, not an error.
            _logger.LogWarning("Request cancelled: {RequestName}", requestName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request: {RequestName}", requestName);
            throw;
        }
    }
}
