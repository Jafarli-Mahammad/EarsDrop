namespace Infrastructure.ProcessRunner;

public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
