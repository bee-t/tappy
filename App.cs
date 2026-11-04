using Tappy.Services;
using Tappy.Views;

namespace Tappy;

public sealed class App : Application
{
    private readonly MainPage _mainPage;
    private readonly IDeviceLockService _deviceLockService;

    public App(MainPage mainPage, IDeviceLockService deviceLockService)
    {
        _mainPage = mainPage;
        _deviceLockService = deviceLockService;
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_mainPage)
        {
            Title = "Tappy"
        };

        window.Activated += (_, _) =>
        {
            _deviceLockService.EnforceImmersiveFullscreen();
        };

        return window;
    }
}
