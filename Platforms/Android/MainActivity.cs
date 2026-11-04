using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.View;

namespace Tappy;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    LockTaskMode = "if_whitelisted",
    ScreenOrientation = ScreenOrientation.UserLandscape,
    ConfigurationChanges = ConfigChanges.ScreenSize
                         | ConfigChanges.Orientation
                         | ConfigChanges.UiMode
                         | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize
                         | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Swallow Android 13+ system back gestures while game is active
        OnBackPressedDispatcher.AddCallback(this, new BabyProofBackPressedCallback(enabled: true));

        ApplyStrictImmersiveFullscreen();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplyStrictImmersiveFullscreen();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            ApplyStrictImmersiveFullscreen();
        }
    }

    private void ApplyStrictImmersiveFullscreen()
    {
        try
        {
            if (Window is null)
            {
                return;
            }

            Window.AddFlags(WindowManagerFlags.KeepScreenOn);

            if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window.Attributes is not null)
            {
                Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            }

            WindowCompat.SetDecorFitsSystemWindows(Window, false);

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                IWindowInsetsController? controller = Window.InsetsController;
                if (controller is not null)
                {
                    controller.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                    controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
            }
            else if (Window.DecorView is not null)
            {
#pragma warning disable CS0618
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
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
            // Ignore transient window decor state transitions
        }
    }

    private sealed class BabyProofBackPressedCallback(bool enabled) : OnBackPressedCallback(enabled)
    {
        public override void HandleOnBackPressed()
        {
            // Intentionally no-op: baby cannot exit via Back button or edge-swipe back gesture.
            // Exiting requires solving the 3-second hold + Mathematical Parent Gate.
        }
    }
}

