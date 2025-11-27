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

builder.Services.Configure<HepsiburadaOptions>(builder.Configuration.GetSection("Hepsiburada"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddHttpClient(nameof(HepsiburadaPriceFetcher), client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
});

builder.Services.AddSingleton<IPriceFetcher, HepsiburadaPriceFetcher>();
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

await builder.Build().RunAsync();
