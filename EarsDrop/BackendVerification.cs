using System;
using System.Threading.Tasks;
using Application;
using Application.UseCases.DownloadMedia;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EarsDrop;

public static class BackendVerification
{
    public static async Task<bool> RunVerificationAsync(string testUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ")
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("   EarsDrop Backend Pipeline Verification (Phase 1)");
        Console.WriteLine("==================================================");

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            Console.WriteLine("[1/5] Initializing SQLite DataContext...");
            var dataContext = serviceProvider.GetRequiredService<DataContext>();
            await dataContext.InitializeDatabaseAsync();
            Console.WriteLine("  -> SQLite Database initialized.");

            Console.WriteLine($"[2/5] Dispatching DownloadMediaCommand for URL: {testUrl}");
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            var command = new DownloadMediaCommand(testUrl, Domain.Enums.OutputFormat.Mp3);
            var result = await mediator.Send(command);

            if (result.IsSuccess)
            {
                Console.WriteLine("  -> Download Pipeline Executed Successfully!");
                Console.WriteLine($"     Job ID: {result.Value.Id}");
                Console.WriteLine($"     Title: {result.Value.Source.Title}");
                Console.WriteLine($"     Uploader: {result.Value.Source.Uploader}");
                Console.WriteLine($"     Output Path: {result.Value.OutputPath}");
                Console.WriteLine($"     Status: {result.Value.Status}");
                return true;
            }
            else
            {
                Console.WriteLine($"  -> Pipeline failed: {result.Error.Code} - {result.Error.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Verification Exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return false;
        }
    }
}
