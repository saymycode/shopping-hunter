using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceWatcher.App.Configuration;
using PriceWatcher.App.Services;
using PriceWatcher.App.Workers;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<PriceWatcherOptions>(builder.Configuration.GetSection("PriceWatcher"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddHttpClient(nameof(HepsiburadaPriceFetcher), client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
    AllowAutoRedirect = true
});

builder.Services.AddHttpClient(nameof(TrendyolPriceFetcher), client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
    AllowAutoRedirect = true
});

builder.Services.AddHttpClient(nameof(AmazonPriceFetcher), client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
    AllowAutoRedirect = true
});

builder.Services.AddSingleton<ISitePriceFetcher, HepsiburadaPriceFetcher>();
builder.Services.AddSingleton<ISitePriceFetcher, TrendyolPriceFetcher>();
builder.Services.AddSingleton<ISitePriceFetcher, AmazonPriceFetcher>();
builder.Services.AddSingleton<IPriceFetcher, MultiStorePriceFetcher>();
builder.Services.AddSingleton<IPriceStorage, LiteDbPriceStorage>();
builder.Services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
builder.Services.AddSingleton<ITelegramUpdateHandler, TelegramUpdateHandler>();
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

builder.Services.AddHostedService<PriceWatcherWorker>();
builder.Services.AddHostedService<TelegramBotWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = false;
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

var host = builder.Build();

// Initialize admin subscriber on startup
using (var scope = host.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<IPriceStorage>();
    var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (long.TryParse(options.AdminChatId, out var adminChatId))
    {
        try
        {
            await storage.AddOrActivateSubscriberAsync(adminChatId, CancellationToken.None);
            logger.LogInformation("Admin chat {AdminChatId} added as subscriber", adminChatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add admin as subscriber");
        }
    }
}

await host.RunAsync();
