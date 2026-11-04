#if ANDROID
using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
#endif

namespace Tappy.Services;

public interface IDeviceLockService
{
    bool IsTaskLocked { get; }
    void StartKioskLockTask();
    void StopKioskLockTask();
    void ExitApplication();
    void EnforceImmersiveFullscreen();
}

public sealed class DeviceLockService : IDeviceLockService
{
    private bool _lockRequested;

    public bool IsTaskLocked
    {
        get
        {
#if ANDROID
            Activity? activity = Platform.CurrentActivity;
            if (activity is not null)
            {
                return IsActivityInLockTaskMode(activity) || _lockRequested;
            }
#endif
            return _lockRequested;
        }
    }

    public void StartKioskLockTask()
    {
#if ANDROID
        try
        {
            Activity? activity = Platform.CurrentActivity;
            if (activity is null)
            {
                return;
            }

            EnforceImmersiveFullscreen();
            TryWhitelistPackageIfDeviceOwner(activity);

            if (!IsActivityInLockTaskMode(activity))
            {
                activity.StartLockTask();
            }

            _lockRequested = true;
        }
        catch
        {
            _lockRequested = false;
        }
#endif
    }

    public void StopKioskLockTask()
    {
        _lockRequested = false;
#if ANDROID
        try
        {
            Activity? activity = Platform.CurrentActivity;
            if (activity is null)
            {
                return;
            }

            activity.StopLockTask();
        }
        catch
        {
            // Ignore if not currently in LockTask state
        }
#endif
    }

    public void ExitApplication()
    {
        StopKioskLockTask();
#if ANDROID
        try
        {
            Activity? activity = Platform.CurrentActivity;
            if (activity is not null)
            {
                activity.FinishAndRemoveTask();
                return;
            }
        }
        catch
        {
            // Fallback to Application.Current.Quit
        }
#endif
        Microsoft.Maui.Controls.Application.Current?.Quit();
    }

    public void EnforceImmersiveFullscreen()
    {
#if ANDROID
        try
        {
            Activity? activity = Platform.CurrentActivity;
            Android.Views.Window? window = activity?.Window;
            if (window is null)
            {
                return;
            }

            window.AddFlags(WindowManagerFlags.KeepScreenOn);

            if (OperatingSystem.IsAndroidVersionAtLeast(28) && window.Attributes is not null)
            {
                window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            }

            WindowCompat.SetDecorFitsSystemWindows(window, false);

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                IWindowInsetsController? controller = window.InsetsController;
                if (controller is not null)
                {
                    controller.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                    controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
            }
            else if (window.DecorView is not null)
            {
#pragma warning disable CS0618
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.Fullscreen);
#pragma warning restore CS0618
            }
        }
        catch
        {
            // Ignore window state transitions
        }
#endif
    }

#if ANDROID
    private static void TryWhitelistPackageIfDeviceOwner(Activity activity)
    {
#if ENTERPRISE_KIOSK
        try
        {
            if (activity.GetSystemService(Context.DevicePolicyService) is not DevicePolicyManager dpm)
            {
                return;
            }

            string? packageName = activity.PackageName;
            if (string.IsNullOrEmpty(packageName) || !dpm.IsDeviceOwnerApp(packageName))
            {
                return;
            }

            var adminComponent = new ComponentName(activity, Java.Lang.Class.FromType(typeof(Platforms.Android.LockTaskDeviceAdminReceiver)));
            dpm.SetLockTaskPackages(adminComponent, [packageName]);
        }
        catch
        {
            // Standard Screen Pinning will be used when app is not Device Owner
        }
#endif
    }

    private static bool IsActivityInLockTaskMode(Activity activity)
    {
        if (activity.GetSystemService(Context.ActivityService) is not ActivityManager am)
        {
            return false;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return am.LockTaskModeState != LockTaskMode.None;
        }

#pragma warning disable CS0618
        return am.IsInLockTaskMode;
#pragma warning restore CS0618
    }
#endif
}
