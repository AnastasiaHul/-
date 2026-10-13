using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SmartTrip.Infrastructure;
using SmartTrip.Infrastructure.Data;

namespace Smart_tripp.WPF;

public partial class App : System.Windows.Application
{
    private ServiceProvider? provider;
    private IServiceScope? scope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string data = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SmartTrip");

        Directory.CreateDirectory(data);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(data, "logs", "smart-trip-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        try
        {
            IConfigurationRoot config =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false)
                    .AddJsonFile(
                        "appsettings.local.json",
                        optional: true)
                    .AddEnvironmentVariables("SMARTTRIP_")
                    .Build();

            string databaseProvider =
                config["DatabaseProvider"] ?? "Sqlite";

            var services = new ServiceCollection();

            services.AddSmartTrip(options =>
            {
                if (databaseProvider.Equals(
                    "Sqlite",
                    StringComparison.OrdinalIgnoreCase))
                {
                    string databasePath =
                        Path.Combine(data, "smart-trip.db");

                    options.UseSqlite(
                        $"Data Source={databasePath}");
                }
                else if (databaseProvider.Equals(
                    "PostgreSQL",
                    StringComparison.OrdinalIgnoreCase))
                {
                    string connection =
                        config.GetConnectionString(
                            "DefaultConnection")
                        ?? throw new InvalidOperationException(
                            "Не задано підключення PostgreSQL.");

                    options.UseNpgsql(connection);
                }
                else
                {
                    throw new InvalidOperationException(
                        "DatabaseProvider: використовуйте "
                        + "Sqlite або PostgreSQL.");
                }
            }, Log.Logger);

            services.AddScoped<MainWindow>();

            provider = services.BuildServiceProvider(
                validateScopes: true);

            scope = provider.CreateScope();

            await DatabaseInitializer.InitializeAsync(
                scope.ServiceProvider.GetRequiredService<
                    IDbContextFactory<ApplicationDbContext>>());

            MainWindow window =
                scope.ServiceProvider
                    .GetRequiredService<MainWindow>();

            MainWindow = window;

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            window.Show();

            Log.Information(
                "Smart Trip started with {DatabaseProvider}",
                databaseProvider);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Startup failed");

            MessageBox.Show(
                "Не вдалося запустити Smart Trip. "
                + "Перевірте налаштування бази даних. "
                + "Деталі у %LOCALAPPDATA%\\SmartTrip\\logs.",
                "Smart Trip",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        scope?.Dispose();
        provider?.Dispose();

        Log.CloseAndFlush();

        base.OnExit(e);
    }
}