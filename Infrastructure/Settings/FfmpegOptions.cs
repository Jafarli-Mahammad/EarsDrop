namespace Infrastructure.Settings;

public class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    public string ExecutablePath { get; set; } = "ffmpeg";

    public string AudioBitrate { get; set; } = "320k";

    public string AudioCodec { get; set; } = "libmp3lame";
}