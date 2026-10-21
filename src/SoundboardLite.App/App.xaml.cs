using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SoundboardLite.App.Services;
using SoundboardLite.App.ViewModels;
using SoundboardLite.App.Views;
using SoundboardLite.Api;
using SoundboardLite.Audio;
using SoundboardLite.Audio.Peaks;
using SoundboardLite.Audio.Pitch;
using SoundboardLite.Audio.Ptt;
using SoundboardLite.Audio.Rendering;
using SoundboardLite.Core.Abstractions;
using SoundboardLite.Core.Services;
using SoundboardLite.Data;
using SoundboardLite.Data.Repositories;
using Wpf.Ui.Appearance;

namespace SoundboardLite.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Last-resort safety net: an uncaught exception on the UI thread would otherwise crash the
        // whole app silently (no dialog, just gone) — log it and keep running instead. This is not a
        // substitute for handling expected failures (missing file, failed render, etc.) at their
        // source with a clear message; it's only for whatever wasn't anticipated.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled exception on the UI thread");
            args.Handled = true;
        };

        var appPaths = new AppPaths();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(appPaths.RootDirectory, "logs", "log-.txt"),
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IAppPaths>(appPaths);
                services.AddSingleton<SqliteConnectionFactory>();

                services.AddSingleton<IBoardRepository, BoardRepository>();
                services.AddSingleton<IClipRepository, ClipRepository>();
                services.AddSingleton<IOriginalFileRepository, OriginalFileRepository>();
                services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();

                services.AddSingleton<IPitchShifter, SoundTouchPitchShifter>();
                services.AddSingleton<IClipRenderer, ClipRenderer>();
                services.AddSingleton<IWaveformPeakService, WaveformPeakService>();
                services.AddSingleton<IRenderQueue, RenderQueue>();
                services.AddSingleton<IRenderCacheGc, RenderCacheGc>();
                services.AddSingleton<IPushToTalkController, PushToTalkController>();

                services.AddSingleton<IClipImportService, ClipImportService>();
                services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();

                services.AddSingleton<EventHub>();
                services.AddSingleton<ApiHostedService>();
                services.AddHostedService<ApiHostedService>(sp => sp.GetRequiredService<ApiHostedService>());

                services.AddSingleton<ClipEditorWindowFactory>();
                services.AddSingleton<SettingsWindowFactory>();

                services.AddSingleton<HttpClient>();
                services.AddSingleton<UpdateCheckService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        ApplicationThemeManager.ApplySystemTheme();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        var playbackCoordinator = _host.Services.GetRequiredService<IPlaybackCoordinator>();
        var viewModel = _host.Services.GetRequiredService<MainViewModel>();

        _ = playbackCoordinator.InitializeAsync()
            .ContinueWith(_ => viewModel.RefreshOutputDevices(), TaskScheduler.FromCurrentSynchronizationContext());
        _ = viewModel.LoadAsync();

        _ = _host.Services.GetRequiredService<IRenderCacheGc>().RunAsync();

        _ = _host.Services.GetRequiredService<UpdateCheckService>().CheckAsync()
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    viewModel.ApplyUpdateCheckResult(t.Result);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
