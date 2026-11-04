using Microsoft.Maui.Graphics;
using Tappy.Models;

namespace Tappy.Graphics;

public sealed class TappyGameDrawable : IDrawable
{
    public const float ParentPillWidth = 168f;
    public const float ParentPillHeight = 46f;

    private struct AmbientStar
    {
        public float NormalizedX;
        public float NormalizedY;
        public float Phase;
        public float Speed;
        public float Radius;
    }

    private readonly AmbientStar[] _ambientStars;

    public TappyGameMode CurrentMode { get; set; } = TappyGameMode.ShapePop;
    public TappyShape? ActiveShape { get; set; }
    public IReadOnlyList<FloatingBubble> Bubbles { get; set; } = Array.Empty<FloatingBubble>();
    public IReadOnlyList<RainbowKeyBar> RainbowKeys { get; set; } = Array.Empty<RainbowKeyBar>();
    public ParticleSystem? Particles { get; set; }
    public ParentControlSheet? ParentSheet { get; set; }

    public float CornerHoldProgress { get; set; }
    public float AmbientTime { get; set; }
    public int ScorePopCount { get; set; }
    public bool IsTaskLocked { get; set; }
    public bool IsAudioMuted { get; set; }
    public bool IsHapticsEnabled { get; set; } = true;

    public TappyGameDrawable()
    {
        var rng = new Random(42);
        _ambientStars = new AmbientStar[28];
        for (int i = 0; i < _ambientStars.Length; i++)
        {
            _ambientStars[i] = new AmbientStar
            {
                NormalizedX = (float)rng.NextDouble(),
                NormalizedY = (float)rng.NextDouble(),
                Phase = (float)(rng.NextDouble() * MathF.Tau),
                Speed = 1.2f + ((float)rng.NextDouble() * 2.0f),
                Radius = 1.4f + ((float)rng.NextDouble() * 2.2f)
            };
        }
    }

    public static RectF GetParentCornerZone(float canvasWidth)
    {
        return new RectF(
            Math.Max(12f, canvasWidth - ParentPillWidth - 16f),
            14f,
            ParentPillWidth,
            ParentPillHeight);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        // 1. Pure Black (#000000) high-contrast background
        canvas.FillColor = Palette.Black;
        canvas.FillRectangle(dirtyRect);

        // 2. Subtle twinkling night stars (ambient depth on black)
        if (CurrentMode != TappyGameMode.RainbowKeys)
        {
            DrawAmbientStars(canvas, dirtyRect);
        }

        // 3. Active Baby Sensory Mode
        switch (CurrentMode)
        {
            case TappyGameMode.ShapePop:
                DrawShapePopMode(canvas);
                break;

            case TappyGameMode.FloatingBubbles:
                DrawFloatingBubblesMode(canvas);
                break;

            case TappyGameMode.StarlightFireworks:
                DrawStarlightFireworksMode(canvas, dirtyRect);
                break;

            case TappyGameMode.RainbowKeys:
                DrawRainbowKeysMode(canvas, dirtyRect);
                break;
        }

        // 4. Zero-allocation particle & shockwave ring bursts
        Particles?.Draw(canvas);

        // 5. Top HUD: Mode indicator + Parent Corner Hold Pill
        DrawTopHud(canvas, dirtyRect);

        // 6. Parent Control Center & Mode Switcher Overlay
        if (ParentSheet is not null)
        {
            DrawParentControlSheet(canvas, dirtyRect, ParentSheet);
        }

        canvas.RestoreState();
    }

    private void DrawAmbientStars(ICanvas canvas, RectF dirtyRect)
    {
        float intensityBoost = CurrentMode == TappyGameMode.StarlightFireworks ? 1.45f : 0.75f;
        for (int i = 0; i < _ambientStars.Length; i++)
        {
            ref readonly AmbientStar star = ref _ambientStars[i];
            float twinkle = 0.2f + (0.4f * (0.5f + (0.5f * MathF.Sin((AmbientTime * star.Speed) + star.Phase))));
            canvas.FillColor = Palette.White.WithAlpha(Math.Clamp(twinkle * intensityBoost, 0.08f, 0.7f));
            canvas.FillCircle(
                star.NormalizedX * dirtyRect.Width,
                star.NormalizedY * dirtyRect.Height,
                star.Radius);
        }
    }

    private void DrawShapePopMode(ICanvas canvas)
    {
        TappyShape? shape = ActiveShape;
        if (shape is null)
        {
            return;
        }

        float scale = shape.CurrentScale;
        float scaledRadius = shape.Radius * scale;
        if (scaledRadius <= 1f)
        {
            return;
        }

        // Soft pulsing outer halo rings
        canvas.FillColor = shape.FillColor.WithAlpha(0.14f);
        canvas.FillCircle(shape.Center, scaledRadius * 1.32f);

        canvas.FillColor = shape.FillColor.WithAlpha(0.24f);
        canvas.FillCircle(shape.Center, scaledRadius * 1.15f);

        // Crisp white contrast outline behind shape for infant visual edge detection
        ShapePainter.DrawFriendly(
            canvas,
            shape.Kind,
            shape.Center.X,
            shape.Center.Y,
            scaledRadius + 5f,
            Palette.White,
            rotationDegrees: shape.CurrentTiltDegrees,
            face: false);

        // Main friendly shape with blinking eyes, blush cheeks, and smile
        ShapePainter.DrawFriendly(
            canvas,
            shape.Kind,
            shape.Center.X,
            shape.Center.Y,
            scaledRadius,
            shape.FillColor,
            rotationDegrees: shape.CurrentTiltDegrees,
            blink: shape.EyeOpenRatio,
            face: true);
    }

    private void DrawFloatingBubblesMode(ICanvas canvas)
    {
        IReadOnlyList<FloatingBubble> bubbles = Bubbles;
        for (int i = 0; i < bubbles.Count; i++)
        {
            FloatingBubble b = bubbles[i];
            float spawnScale = Ease.OutBack(Ease.Clamp01(b.SpawnAge / 0.3f), 1.4f);
            float wobbleX = 1f + (0.04f * MathF.Sin((b.Phase * 2.2f) + i));
            float wobbleY = 1f - (0.04f * MathF.Sin((b.Phase * 2.2f) + i));
            float r = b.Radius * spawnScale;
            if (r <= 2f)
            {
                continue;
            }

            canvas.SaveState();
            canvas.Translate(b.Center.X, b.Center.Y);
            canvas.Scale(wobbleX, wobbleY);

            // Outer iridescent glow
            canvas.FillColor = b.RimColor.WithAlpha(0.16f);
            canvas.FillCircle(0f, 0f, r * 1.12f);

            // Translucent soap bubble body
            canvas.FillColor = b.RimColor.WithAlpha(0.24f);
            canvas.FillCircle(0f, 0f, r);

            // Cute mini shape buddy floating inside the bubble
            ShapePainter.DrawFriendly(
                canvas,
                b.InnerBuddyKind,
                0f,
                0f,
                r * 0.52f,
                b.RimColor,
                rotationDegrees: MathF.Sin(b.Phase) * 10f,
                face: true);

            // High-contrast iridescent bubble rim
            canvas.StrokeColor = Palette.Lighten(b.RimColor, 0.35f);
            canvas.StrokeSize = 4.5f;
            canvas.DrawCircle(0f, 0f, r);

            // Specular soap bubble highlight crescents
            canvas.FillColor = Palette.White.WithAlpha(0.75f);
            canvas.FillCircle(-r * 0.38f, -r * 0.38f, r * 0.16f);
            canvas.FillColor = Palette.White.WithAlpha(0.45f);
            canvas.FillCircle(-r * 0.16f, -r * 0.52f, r * 0.08f);

            canvas.RestoreState();
        }
    }

    private void DrawStarlightFireworksMode(ICanvas canvas, RectF dirtyRect)
    {
        PointF center = dirtyRect.Center;
        float pulse = 1f + (0.06f * MathF.Sin(AmbientTime * 2.6f));
        float baseRadius = 64f * pulse;

        canvas.FillColor = Palette.Amber.WithAlpha(0.14f);
        canvas.FillCircle(center, baseRadius * 1.55f);

        ShapePainter.DrawFriendly(
            canvas,
            ShapeKind.Star,
            center.X,
            center.Y,
            baseRadius,
            Palette.Yellow,
            rotationDegrees: MathF.Sin(AmbientTime * 1.3f) * 8f,
            blink: (MathF.Sin(AmbientTime * 1.9f) > 0.92f) ? 0.12f : 1f,
            face: true);

        canvas.FontColor = Palette.TextSecondary.WithAlpha(0.55f);
        canvas.FontSize = 15f;
        canvas.DrawString(
            "Tap anywhere with one or more fingers for Starlight Fireworks!",
            new RectF(0f, dirtyRect.Height - 46f, dirtyRect.Width, 30f),
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawRainbowKeysMode(ICanvas canvas, RectF dirtyRect)
    {
        IReadOnlyList<RainbowKeyBar> keys = RainbowKeys;
        if (keys.Count == 0)
        {
            return;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            RainbowKeyBar key = keys[i];
            RectF rect = key.Bounds;
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                continue;
            }

            float bounceOffset = key.PressBounce * 10f;
            var drawRect = new RectF(
                rect.Left,
                rect.Top + bounceOffset,
                rect.Width,
                rect.Height - bounceOffset);

            canvas.FillColor = Palette.Darken(key.BaseColor, 0.45f);
            canvas.FillRoundedRectangle(new RectF(rect.Left, rect.Top + 8f, rect.Width, rect.Height - 4f), 22f);

            Color bodyColor = key.GlowAlpha > 0.01f
                ? Palette.Lighten(key.BaseColor, key.GlowAlpha * 0.42f)
                : key.BaseColor;
            canvas.FillColor = bodyColor;
            canvas.FillRoundedRectangle(drawRect, 22f);

            canvas.StrokeColor = Palette.White.WithAlpha(0.35f + (key.GlowAlpha * 0.65f));
            canvas.StrokeSize = 3f + (key.GlowAlpha * 3f);
            canvas.DrawRoundedRectangle(drawRect, 22f);

            canvas.FillColor = Palette.White.WithAlpha(0.22f);
            canvas.FillRoundedRectangle(
                new RectF(drawRect.Left + 10f, drawRect.Top + 10f, drawRect.Width - 20f, 26f),
                12f);

            float buddyRadius = Math.Min(drawRect.Width * 0.36f, 48f) * (1f + (key.GlowAlpha * 0.18f));
            ShapePainter.DrawFriendly(
                canvas,
                key.BuddyKind,
                drawRect.Center.X,
                drawRect.Center.Y,
                buddyRadius,
                Palette.Lighten(key.BaseColor, 0.25f),
                rotationDegrees: key.GlowAlpha * 12f,
                face: true);

            var noteRect = new RectF(drawRect.Left, drawRect.Bottom - 54f, drawRect.Width, 36f);
            canvas.FontColor = Palette.FaceInk.WithAlpha(0.85f);
            canvas.FontSize = 20f;
            canvas.DrawString(
                key.NoteLabel,
                noteRect,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private void DrawTopHud(ICanvas canvas, RectF dirtyRect)
    {
        GameModeMetadata modeInfo = GameModeCatalog.All[(int)CurrentMode];
        var modeBadgeRect = new RectF(16f, 14f, 210f, 40f);
        canvas.FillColor = Palette.Surface.WithAlpha(0.75f);
        canvas.StrokeColor = modeInfo.AccentColor.WithAlpha(0.55f);
        canvas.StrokeSize = 1.5f;
        canvas.FillRoundedRectangle(modeBadgeRect, 20f);
        canvas.DrawRoundedRectangle(modeBadgeRect, 20f);

        canvas.FontColor = Palette.TextPrimary.WithAlpha(0.88f);
        canvas.FontSize = 13f;
        canvas.DrawString(
            $"{modeInfo.Title}  •  ★ {ScorePopCount}",
            modeBadgeRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        RectF cornerRect = GetParentCornerZone(dirtyRect.Width);
        canvas.FillColor = Palette.Surface.WithAlpha(0.85f);
        canvas.StrokeColor = CornerHoldProgress > 0.01f
            ? Palette.Success
            : Palette.SurfaceBorder;
        canvas.StrokeSize = CornerHoldProgress > 0.01f ? 2.5f : 1.5f;
        canvas.FillRoundedRectangle(cornerRect, 23f);
        canvas.DrawRoundedRectangle(cornerRect, 23f);

        if (CornerHoldProgress > 0.01f)
        {
            var fillRect = new RectF(
                cornerRect.Left + 3f,
                cornerRect.Top + 3f,
                (cornerRect.Width - 6f) * CornerHoldProgress,
                cornerRect.Height - 6f);
            canvas.FillColor = Palette.Success.WithAlpha(0.32f);
            canvas.FillRoundedRectangle(fillRect, 20f);
        }

        canvas.FillColor = IsTaskLocked ? Palette.Success : Palette.Amber;
        canvas.FillCircle(cornerRect.Left + 20f, cornerRect.Center.Y, 5.5f);

        canvas.FontColor = Palette.TextPrimary;
        canvas.FontSize = 12.5f;
        string pillText = CornerHoldProgress > 0.01f
            ? "HOLDING..."
            : (IsTaskLocked ? "PARENT (HOLD)" : "PARENT MENU");
        canvas.DrawString(
            pillText,
            new RectF(cornerRect.Left + 30f, cornerRect.Top, cornerRect.Width - 36f, cornerRect.Height),
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawParentControlSheet(ICanvas canvas, RectF dirtyRect, ParentControlSheet sheet)
    {
        canvas.FillColor = Palette.HomeBottom.WithAlpha(0.92f);
        canvas.FillRectangle(dirtyRect);

        float panelW = Math.Min(680f, dirtyRect.Width - 32f);
        float panelH = Math.Min(390f, dirtyRect.Height - 24f);
        float panelX = dirtyRect.Center.X - (panelW * 0.5f);
        float panelY = dirtyRect.Center.Y - (panelH * 0.5f);
        var panelRect = new RectF(panelX, panelY, panelW, panelH);

        canvas.FillColor = Palette.Surface;
        canvas.StrokeColor = Palette.Accent.WithAlpha(0.7f);
        canvas.StrokeSize = 2.5f;
        canvas.FillRoundedRectangle(panelRect, 24f);
        canvas.DrawRoundedRectangle(panelRect, 24f);

        canvas.FontColor = Palette.TextPrimary;
        canvas.FontSize = 20f;
        canvas.DrawString(
            "Tappy  •  Parent Hub",
            new RectF(panelX + 22f, panelY + 14f, panelW * 0.55f, 30f),
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        string pinStatusText = IsTaskLocked
            ? "Screen Pinning: ACTIVE (Baby-Proofed)"
            : "Screen Pinning: UNPINNED (Navigation Unlocked)";
        canvas.FontColor = IsTaskLocked ? Palette.Success : Palette.Amber;
        canvas.FontSize = 12.5f;
        canvas.DrawString(
            pinStatusText,
            new RectF(panelX + 22f, panelY + 42f, panelW * 0.6f, 20f),
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        var resumeRect = new RectF(panelRect.Right - 162f, panelY + 16f, 142f, 42f);
        sheet.ResumePlayButtonRect = resumeRect;
        canvas.FillColor = Palette.Accent;
        canvas.FillRoundedRectangle(resumeRect, 21f);
        canvas.FontColor = Palette.Black;
        canvas.FontSize = 14f;
        canvas.DrawString(
            "▶ RESUME PLAY",
            resumeRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        if (!sheet.IsGateUnlocked)
        {
            DrawQuickSequenceGate(canvas, panelRect, sheet);
            return;
        }

        float cardsTop = panelY + 72f;
        float cardsPadding = 18f;
        float cardGap = 12f;
        float totalCardsW = panelW - (cardsPadding * 2f);
        float cardW = (totalCardsW - (cardGap * 3f)) / 4f;
        float cardH = Math.Min(156f, panelH - 196f);

        for (int i = 0; i < GameModeCatalog.All.Count; i++)
        {
            GameModeMetadata mode = GameModeCatalog.All[i];
            bool isSelected = mode.Mode == CurrentMode;
            float cx = panelX + cardsPadding + (i * (cardW + cardGap));
            var cardRect = new RectF(cx, cardsTop, cardW, cardH);
            sheet.ModeCardRects[i] = cardRect;

            canvas.FillColor = isSelected ? Palette.SurfaceRaised : Palette.HomeBottom.WithAlpha(0.75f);
            canvas.StrokeColor = isSelected ? mode.AccentColor : Palette.SurfaceBorder;
            canvas.StrokeSize = isSelected ? 3f : 1.5f;
            canvas.FillRoundedRectangle(cardRect, 18f);
            canvas.DrawRoundedRectangle(cardRect, 18f);

            ShapePainter.DrawFriendly(
                canvas,
                mode.IconKind,
                cardRect.Center.X,
                cardRect.Top + (cardH * 0.34f),
                Math.Min(26f, cardH * 0.2f),
                mode.AccentColor,
                face: true);

            canvas.FontColor = Palette.TextPrimary;
            canvas.FontSize = 14f;
            canvas.DrawString(
                mode.Title,
                new RectF(cardRect.Left + 6f, cardRect.Top + (cardH * 0.60f), cardRect.Width - 12f, 22f),
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            canvas.FontColor = isSelected ? mode.AccentColor : Palette.TextSecondary;
            canvas.FontSize = 11f;
            canvas.DrawString(
                isSelected ? "● ACTIVE MODE" : "Tap to Start",
                new RectF(cardRect.Left + 6f, cardRect.Top + (cardH * 0.78f), cardRect.Width - 12f, 20f),
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }

        float bottomBarTop = cardsTop + cardH + 16f;
        float bottomBtnH = Math.Min(54f, panelRect.Bottom - bottomBarTop - 14f);
        float bottomBtnW = (totalCardsW - (cardGap * 3f)) / 4f;

        var soundRect = new RectF(panelX + cardsPadding, bottomBarTop, bottomBtnW, bottomBtnH);
        sheet.SoundToggleRect = soundRect;
        DrawActionButton(
            canvas,
            soundRect,
            IsAudioMuted ? "Sound: MUTED" : "Sound: ON",
            IsAudioMuted ? Palette.SurfaceRaised : Palette.Success.WithAlpha(0.22f),
            IsAudioMuted ? Palette.SurfaceBorder : Palette.Success,
            Palette.TextPrimary);

        var hapticRect = new RectF(soundRect.Right + cardGap, bottomBarTop, bottomBtnW, bottomBtnH);
        sheet.HapticToggleRect = hapticRect;
        DrawActionButton(
            canvas,
            hapticRect,
            IsHapticsEnabled ? "Haptics: ON" : "Haptics: OFF",
            IsHapticsEnabled ? Palette.Success.WithAlpha(0.22f) : Palette.SurfaceRaised,
            IsHapticsEnabled ? Palette.Success : Palette.SurfaceBorder,
            Palette.TextPrimary);

        var unpinRect = new RectF(hapticRect.Right + cardGap, bottomBarTop, bottomBtnW, bottomBtnH);
        sheet.UnpinButtonRect = unpinRect;
        DrawActionButton(
            canvas,
            unpinRect,
            IsTaskLocked ? "UNPIN SCREEN" : "PIN SCREEN",
            IsTaskLocked ? Palette.Amber.WithAlpha(0.25f) : Palette.Accent.WithAlpha(0.25f),
            IsTaskLocked ? Palette.Amber : Palette.Accent,
            Palette.TextPrimary);

        var exitRect = new RectF(unpinRect.Right + cardGap, bottomBarTop, bottomBtnW, bottomBtnH);
        sheet.ExitAppButtonRect = exitRect;
        DrawActionButton(
            canvas,
            exitRect,
            "UNPIN & EXIT",
            Palette.Danger.WithAlpha(0.28f),
            Palette.Danger,
            Palette.TextPrimary);
    }

    private static void DrawQuickSequenceGate(ICanvas canvas, RectF panelRect, ParentControlSheet sheet)
    {
        canvas.FontColor = Palette.TextPrimary;
        canvas.FontSize = 18f;
        canvas.DrawString(
            "Parent Check: Tap 1 → 2 → 3 in order to unlock controls & unpin",
            new RectF(panelRect.Left + 24f, panelRect.Top + 88f, panelRect.Width - 48f, 32f),
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.FontColor = Palette.TextSecondary;
        canvas.FontSize = 13f;
        canvas.DrawString(
            "Prevents accidental baby taps while making it effortless for parents to exit",
            new RectF(panelRect.Left + 24f, panelRect.Top + 120f, panelRect.Width - 48f, 24f),
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        float btnSize = 92f;
        float spacing = 34f;
        float totalW = (btnSize * 3f) + (spacing * 2f);
        float startX = panelRect.Center.X - (totalW * 0.5f);
        float topY = panelRect.Top + 166f;

        Color[] stepColors = [Palette.Sky, Palette.Amber, Palette.Green];

        for (int i = 0; i < 3; i++)
        {
            int stepNumber = i + 1;
            bool isCompleted = stepNumber < sheet.GateNextStep;
            bool isCurrent = stepNumber == sheet.GateNextStep;

            var btnRect = new RectF(startX + (i * (btnSize + spacing)), topY, btnSize, btnSize);
            sheet.GateStepRects[i] = btnRect;

            canvas.FillColor = isCompleted
                ? Palette.Success.WithAlpha(0.35f)
                : (isCurrent ? stepColors[i].WithAlpha(0.28f) : Palette.SurfaceRaised);
            canvas.StrokeColor = isCompleted
                ? Palette.Success
                : (isCurrent ? stepColors[i] : Palette.SurfaceBorder);
            canvas.StrokeSize = isCurrent ? 3.5f : 2f;
            canvas.FillRoundedRectangle(btnRect, 24f);
            canvas.DrawRoundedRectangle(btnRect, 24f);

            canvas.FontColor = Palette.TextPrimary;
            canvas.FontSize = 30f;
            canvas.DrawString(
                isCompleted ? "✓" : stepNumber.ToString(),
                btnRect,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private static void DrawActionButton(
        ICanvas canvas,
        RectF rect,
        string label,
        Color fill,
        Color stroke,
        Color textColor)
    {
        canvas.FillColor = fill;
        canvas.StrokeColor = stroke;
        canvas.StrokeSize = 2f;
        canvas.FillRoundedRectangle(rect, 14f);
        canvas.DrawRoundedRectangle(rect, 14f);

        canvas.FontColor = textColor;
        canvas.FontSize = 13.5f;
        canvas.DrawString(label, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

