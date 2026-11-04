using Microsoft.Extensions.Logging;
using Tappy.Services;
using Tappy.Views;

namespace Tappy;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        // Register Core Sensory Services & Single-Screen Game Loop Page
        builder.Services.AddSingleton<ITappyAudioService, TappyAudioService>();
        builder.Services.AddSingleton<IHapticService, HapticService>();
        builder.Services.AddSingleton<IDeviceLockService, DeviceLockService>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
