namespace Tappy.Graphics;

/// <summary>
/// Central color system. Play scenes use vivid, high-contrast hues on pure black;
/// the parent-facing home screen uses a calm deep-navy surface palette.
/// </summary>
public static class Palette
{
    public static readonly Color Black = Color.FromArgb("#000000");
    public static readonly Color White = Color.FromArgb("#FFFFFF");

    // Parent / UI surfaces
    public static readonly Color HomeTop = Color.FromArgb("#171D3A");
    public static readonly Color HomeBottom = Color.FromArgb("#06070F");
    public static readonly Color Surface = Color.FromArgb("#151A31");
    public static readonly Color SurfaceRaised = Color.FromArgb("#232B4D");
    public static readonly Color SurfaceBorder = Color.FromArgb("#2D365E");
    public static readonly Color TextPrimary = Color.FromArgb("#F5F7FF");
    public static readonly Color TextSecondary = Color.FromArgb("#9AA3C7");
    public static readonly Color TextMuted = Color.FromArgb("#616A92");
    public static readonly Color Accent = Color.FromArgb("#7C9CFF");
    public static readonly Color Success = Color.FromArgb("#3DDC97");
    public static readonly Color Danger = Color.FromArgb("#FF6B81");

    // Character details
    public static readonly Color FaceInk = Color.FromArgb("#1B1033");
    public static readonly Color Blush = Color.FromArgb("#FF8FB1");

    // Vivid infant palette (high saturation, distinct hues)
    public static readonly Color Pink = Color.FromArgb("#FF4D8D");
    public static readonly Color Red = Color.FromArgb("#FF5C5C");
    public static readonly Color Orange = Color.FromArgb("#FF8A3D");
    public static readonly Color Amber = Color.FromArgb("#FFB627");
    public static readonly Color Yellow = Color.FromArgb("#FFE45C");
    public static readonly Color Green = Color.FromArgb("#3DDC97");
    public static readonly Color Sky = Color.FromArgb("#2EC4FF");
    public static readonly Color Blue = Color.FromArgb("#4D7CFF");
    public static readonly Color Violet = Color.FromArgb("#9B6BFF");
    public static readonly Color Magenta = Color.FromArgb("#E04DFF");

    public static readonly Color[] Vivid = [Pink, Orange, Amber, Yellow, Green, Sky, Blue, Violet];

    public static Color Pick(Random rng, Color? avoid = null)
    {
        Color candidate;
        do
        {
            candidate = Vivid[rng.Next(Vivid.Length)];
        }
        while (avoid is not null && ReferenceEquals(candidate, avoid));

        return candidate;
    }

    public static Color Lighten(Color c, float amount) => new(
        c.Red + ((1f - c.Red) * amount),
        c.Green + ((1f - c.Green) * amount),
        c.Blue + ((1f - c.Blue) * amount),
        c.Alpha);

    public static Color Darken(Color c, float amount) => new(
        c.Red * (1f - amount),
        c.Green * (1f - amount),
        c.Blue * (1f - amount),
        c.Alpha);
}

