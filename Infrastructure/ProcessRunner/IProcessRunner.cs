namespace Infrastructure.ProcessRunner;

public interface IProcessRunner
{
    Task<ProcessResult> ExecuteAsync(
        string executablePath,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}