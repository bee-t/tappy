#if ENTERPRISE_KIOSK
using Android.App;
using Android.App.Admin;
using Android.Content;

namespace Tappy.Platforms.Android;

/// <summary>
/// Optional enterprise DeviceAdminReceiver enabled only when building with -p:DefineConstants=ENTERPRISE_KIOSK.
/// Excluded from standard Google Play Store builds so the app passes Google Play Families review
/// without requesting BIND_DEVICE_ADMIN.
/// </summary>
[BroadcastReceiver(
    Name = "com.tappy.sensorygame.LockTaskDeviceAdminReceiver",
    Label = "Tappy LockTask Admin",
    Permission = "android.permission.BIND_DEVICE_ADMIN",
    Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/device_admin_receiver")]
[IntentFilter([DeviceAdminReceiver.ActionDeviceAdminEnabled])]
public sealed class LockTaskDeviceAdminReceiver : DeviceAdminReceiver
{
    public override void OnEnabled(Context context, Intent intent)
    {
        base.OnEnabled(context, intent);
    }
}
#endif
