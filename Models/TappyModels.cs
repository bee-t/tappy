using Microsoft.Maui.Graphics;
using Tappy.Graphics;

namespace Tappy.Models;

public enum TappyGameMode
{
    ShapePop = 0,
    FloatingBubbles = 1,
    StarlightFireworks = 2,
    RainbowKeys = 3
}

public sealed record GameModeMetadata(
    TappyGameMode Mode,
    string Title,
    string Subtitle,
    Color AccentColor,
    ShapeKind IconKind);

public static class GameModeCatalog
{
    public static readonly IReadOnlyList<GameModeMetadata> All =
    [
        new(
            TappyGameMode.ShapePop,
            "Shape Buddies",
            "Big smiling shapes pop & jump on touch",
            Palette.Sky,
            ShapeKind.Star),
        new(
            TappyGameMode.FloatingBubbles,
            "Bubble Drift",
            "Gentle floating bubbles for multi-finger pops",
            Palette.Pink,
            ShapeKind.Circle),
        new(
            TappyGameMode.StarlightFireworks,
            "Starlight Tap",
            "Every tap anywhere bursts into glowing stars",
            Palette.Amber,
            ShapeKind.Heart),
        new(
            TappyGameMode.RainbowKeys,
            "Rainbow Chimes",
            "Giant musical xylophone keys with happy faces",
            Palette.Green,
            ShapeKind.Square)
    ];
}

/// <summary>
/// Represents the active high-contrast geometric buddy in ShapePop mode.
/// Minimum width/diameter is enforced at >= 150 DIPs (exceeding the 120-unit minimum).
/// </summary>
public sealed class TappyShape
{
    public const float MinDiameterUnits = 150f;
    public const float MaxDiameterUnits = 190f;

    public PointF Center { get; set; }
    public float Diameter { get; init; } = 165f;
    public float Radius => Diameter * 0.5f;
    public Color FillColor { get; init; } = Palette.Sky;
    public ShapeKind Kind { get; init; } = ShapeKind.Circle;
    public int NoteIndex { get; init; }

    // Smooth animation state updated at 60fps
    public float SpawnAgeSeconds { get; set; }
    public float BreathPhase { get; set; }
    public float BlinkCountdown { get; set; } = 2.6f;
    public float BlinkDurationRemaining { get; set; }

    public float CurrentScale
    {
        get
        {
            float spawnT = Ease.Clamp01(SpawnAgeSeconds / 0.28f);
            float spring = Ease.OutBack(spawnT, 1.85f);
            float breath = 1f + (0.045f * MathF.Sin(BreathPhase));
            return spring * breath;
        }
    }

    public float CurrentTiltDegrees => 6.5f * MathF.Sin(BreathPhase * 0.7f);

    public float EyeOpenRatio => BlinkDurationRemaining > 0f ? 0.12f : 1.0f;

    public bool ContainsPoint(PointF touchPoint, float touchSlop = 26f)
    {
        float effectiveRadius = Radius + touchSlop;
        float dx = touchPoint.X - Center.X;
        float dy = touchPoint.Y - Center.Y;
        return (dx * dx) + (dy * dy) <= (effectiveRadius * effectiveRadius);
    }
}

/// <summary>
/// Drifting iridescent soap bubble for FloatingBubbles mode.
/// </summary>
public sealed class FloatingBubble
{
    public PointF Center { get; set; }
    public float Radius { get; init; } = 68f;
    public float RiseSpeed { get; init; } = 48f;
    public float SwayAmplitude { get; init; } = 24f;
    public float SwayFrequency { get; init; } = 1.6f;
    public float Phase { get; set; }
    public float SpawnAge { get; set; }
    public Color RimColor { get; init; } = Palette.Sky;
    public ShapeKind InnerBuddyKind { get; init; } = ShapeKind.Star;
    public int NoteIndex { get; init; }

    public bool ContainsPoint(PointF touchPoint, float touchSlop = 22f)
    {
        float r = Radius + touchSlop;
        float dx = touchPoint.X - Center.X;
        float dy = touchPoint.Y - Center.Y;
        return (dx * dx) + (dy * dy) <= (r * r);
    }
}

/// <summary>
/// Giant musical bar for RainbowKeys mode.
/// </summary>
public sealed class RainbowKeyBar
{
    public int KeyIndex { get; init; }
    public string NoteLabel { get; init; } = string.Empty;
    public Color BaseColor { get; init; } = Palette.Pink;
    public ShapeKind BuddyKind { get; init; } = ShapeKind.Circle;
    public RectF Bounds { get; set; }
    public float PressBounce { get; set; }
    public float GlowAlpha { get; set; }
}

/// <summary>
/// Clean, parent-friendly control center and mode switcher overlay.
/// Features a quick 2-step parent unlock bar (or direct controls when already unlocked)
/// plus 1-tap Unpin Screen and Unpin and Exit buttons.
/// </summary>
public sealed class ParentControlSheet
{
    public bool IsGateUnlocked { get; set; }
    public int GateNextStep { get; set; } = 1; // 1 -> 2 -> 3 sequential tap gate
    public float IdleSeconds { get; set; }
    public const float AutoCloseTimeoutSeconds = 12f;

    public RectF[] GateStepRects { get; } = new RectF[3];
    public RectF[] ModeCardRects { get; } = new RectF[4];
    public RectF SoundToggleRect { get; set; }
    public RectF HapticToggleRect { get; set; }
    public RectF UnpinButtonRect { get; set; }
    public RectF ExitAppButtonRect { get; set; }
    public RectF ResumePlayButtonRect { get; set; }
}

