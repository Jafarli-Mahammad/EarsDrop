using Domain.Repositories;
using Infrastructure.Converters;
using Infrastructure.Downloaders;
using Infrastructure.Metadata;
using Infrastructure.Persistence;
using Infrastructure.ProcessRunner;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options pattern
        services.Configure<YtDlpOptions>(configuration.GetSection(YtDlpOptions.SectionName));
        services.Configure<FfmpegOptions>(configuration.GetSection(FfmpegOptions.SectionName));
        services.Configure<MusicBrainzOptions>(configuration.GetSection(MusicBrainzOptions.SectionName));
        services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.SectionName));

        // Process Runner
        services.AddTransient<IProcessRunner, ProcessRunner.ProcessRunner>();

        // Services
        services.AddHttpClient<IMediaDownloader, YtDlpMediaDownloader>();
        services.AddTransient<IMediaConverter, FfmpegMediaConverter>();
        services.AddHttpClient<IMetadataProvider, MusicBrainzMetadataProvider>();
        services.AddTransient<IMetadataWriter, Mp3MetadataWriter>();

        // Persistence
        services.AddSingleton<DataContext>();
        services.AddScoped<IDownloadJobRepository, DownloadJobRepository>();

        return services;
    }
}