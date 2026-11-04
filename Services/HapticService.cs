#if ANDROID
using Android.Content;
using Android.OS;
#endif

namespace Tappy.Services;

public interface IHapticService
{
    bool IsEnabled { get; set; }
    void TriggerShapePopHaptic();
    void TriggerGentleTapHaptic();
    void TriggerParentGateSuccessHaptic();
}

public sealed class HapticService : IHapticService
{
    public bool IsEnabled { get; set; } = true;

    public void TriggerShapePopHaptic()
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
#if ANDROID
            if (TriggerAndroidNativeVibration(durationMs: 36, amplitude: 210))
            {
                return;
            }
#endif
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
        }
        catch
        {
            // Ignore hardware unavailability
        }
    }

    public void TriggerGentleTapHaptic()
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
#if ANDROID
            if (TriggerAndroidNativeVibration(durationMs: 18, amplitude: 140))
            {
                return;
            }
#endif
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
        }
        catch
        {
            // Ignore hardware unavailability
        }
    }

    public void TriggerParentGateSuccessHaptic()
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
#if ANDROID
            if (TriggerAndroidNativeVibration(durationMs: 75, amplitude: 255))
            {
                return;
            }
#endif
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
            }
        }
        catch
        {
            // Ignore hardware unavailability
        }
    }

#if ANDROID
    private static bool TriggerAndroidNativeVibration(long durationMs, int amplitude)
    {
        Context? context = Platform.AppContext;
        if (context is null)
        {
            return false;
        }

        Vibrator? vibrator = null;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var manager = context.GetSystemService(Context.VibratorManagerService) as VibratorManager;
            vibrator = manager?.DefaultVibrator;
        }
        else
        {
#pragma warning disable CS0618
            vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CS0618
        }

        if (vibrator is null || !vibrator.HasVibrator)
        {
            return false;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var effect = VibrationEffect.CreateOneShot(durationMs, amplitude);
            vibrator.Vibrate(effect);
        }
        else
        {
#pragma warning disable CS0618
            vibrator.Vibrate(durationMs);
#pragma warning restore CS0618
        }

        return true;
    }
#endif
}
