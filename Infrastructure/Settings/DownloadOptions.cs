namespace Infrastructure.Settings;

public class DownloadOptions
{
    public const string SectionName = "Download";

    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "EarsDrop");

    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EarsDrop",
        "earsdrop.db");
}
