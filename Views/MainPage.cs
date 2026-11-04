using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Tappy.Graphics;
using Tappy.Models;
using Tappy.Services;
#if ANDROID
using Android.Views;
#endif

namespace Tappy.Views;

/// <summary>
/// Single-screen, XAML-free ContentPage hosting a unified Microsoft.Maui.Graphics.GraphicsView.
/// Supports 4 infant sensory modes, 60fps procedural animations, multi-finger Down-only touch
/// handling, and an intuitive Parent Hub for mode switching and effortless screen unpinning.
/// </summary>
public sealed class MainPage : ContentPage
{
    private const int ParentCornerHoldDurationMs = 1500;
    private const int MaxFloatingBubbles = 7;

    private static readonly string[] SolfegeLabels = ["DO", "RE", "MI", "SOL", "LA", "DO+"];
    private static readonly Color[] KeyPalette =
    [
        Palette.Pink,
        Palette.Orange,
        Palette.Yellow,
        Palette.Green,
        Palette.Sky,
        Palette.Violet
    ];

    private readonly ITappyAudioService _audioService;
    private readonly IHapticService _hapticService;
    private readonly IDeviceLockService _deviceLockService;
    private readonly TappyGameDrawable _drawable;
    private readonly GraphicsView _graphicsView;
    private readonly IDispatcherTimer _frameTimer;
    private readonly Random _rng = new();
    private readonly ParticleSystem _particles;

    private readonly List<FloatingBubble> _bubbles = new(MaxFloatingBubbles);
    private readonly List<RainbowKeyBar> _rainbowKeys = new(6);

    private TappyGameMode _currentMode = TappyGameMode.ShapePop;
    private TappyShape? _currentShape;
    private ParentControlSheet? _parentSheet;
    private Color? _lastShapeColor;
    private int _rollingNoteIndex;
    private int _scorePopCount;

    private long _lastFrameTickMs;
    private int? _cornerHoldPointerId;
    private long _cornerHoldStartTickMs;

#if ANDROID
    private bool _useAndroidNativeMultiTouch;
#endif

    public MainPage(
        ITappyAudioService audioService,
        IHapticService hapticService,
        IDeviceLockService deviceLockService)
    {
        _audioService = audioService;
        _hapticService = hapticService;
        _deviceLockService = deviceLockService;
        _particles = new ParticleSystem(_rng);

        BackgroundColor = Palette.Black;
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeRainbowKeys();

        _parentSheet = new ParentControlSheet
        {
            IsGateUnlocked = true,
            GateNextStep = 4
        };

        _drawable = new TappyGameDrawable
        {
            CurrentMode = _currentMode,
            Particles = _particles,
            Bubbles = _bubbles,
            RainbowKeys = _rainbowKeys,
            ParentSheet = _parentSheet
        };

        _graphicsView = new GraphicsView
        {
            Drawable = _drawable,
            BackgroundColor = Palette.Black,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _graphicsView.StartInteraction += OnGraphicsViewStartInteraction;
        _graphicsView.EndInteraction += OnGraphicsViewEndInteraction;
        _graphicsView.CancelInteraction += OnGraphicsViewCancelInteraction;
        _graphicsView.SizeChanged += OnCanvasSizeChanged;
        _graphicsView.HandlerChanged += OnGraphicsViewHandlerChanged;

        _lastFrameTickMs = Environment.TickCount64;
        _frameTimer = Dispatcher.CreateTimer();
        _frameTimer.Interval = TimeSpan.FromMilliseconds(16);
        _frameTimer.Tick += OnFrameTimerTick;

        Content = _graphicsView;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _deviceLockService.EnforceImmersiveFullscreen();
        SyncDrawableFlags();

        if (!_frameTimer.IsRunning)
        {
            _lastFrameTickMs = Environment.TickCount64;
            _frameTimer.Start();
        }
    }

    protected override bool OnBackButtonPressed() => true;

    private void InitializeRainbowKeys()
    {
        _rainbowKeys.Clear();
        for (int i = 0; i < 6; i++)
        {
            _rainbowKeys.Add(new RainbowKeyBar
            {
                KeyIndex = i,
                NoteLabel = SolfegeLabels[i],
                BaseColor = KeyPalette[i],
                BuddyKind = ShapePainter.AllKinds[i % ShapePainter.AllKinds.Length]
            });
        }
    }

    private void LayoutRainbowKeys()
    {
        float width = (float)_graphicsView.Width;
        float height = (float)_graphicsView.Height;
        if (width <= 0f || height <= 0f || _rainbowKeys.Count == 0)
        {
            return;
        }

        float horizontalMargin = 20f;
        float topMargin = 68f;
        float bottomMargin = 20f;
        float gap = 12f;
        int count = _rainbowKeys.Count;

        float totalW = width - (horizontalMargin * 2f);
        float keyW = Math.Max(40f, (totalW - (gap * (count - 1))) / count);
        float keyH = Math.Max(120f, height - topMargin - bottomMargin);

        for (int i = 0; i < count; i++)
        {
            float left = horizontalMargin + (i * (keyW + gap));
            _rainbowKeys[i].Bounds = new RectF(left, topMargin, keyW, keyH);
        }
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (_graphicsView.Width <= 0 || _graphicsView.Height <= 0)
        {
            return;
        }

        LayoutRainbowKeys();

        if (_currentShape is null)
        {
            SpawnNextShape(previousCenter: null);
        }

        if (_bubbles.Count == 0)
        {
            PopulateInitialBubbles();
        }
    }

    private void OnGraphicsViewHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (_graphicsView.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Touch -= OnAndroidNativeViewTouch;
            nativeView.Touch += OnAndroidNativeViewTouch;
            _useAndroidNativeMultiTouch = true;
        }
#endif
    }

#if ANDROID
    private void OnAndroidNativeViewTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        MotionEvent? motionEvent = e.Event;
        if (motionEvent is null)
        {
            e.Handled = false;
            return;
        }

        float density = (float)DeviceDisplay.Current.MainDisplayInfo.Density;
        if (density <= 0f)
        {
            density = 1f;
        }

        MotionEventActions action = motionEvent.ActionMasked;
        int actionIndex = motionEvent.ActionIndex;
        int pointerId = motionEvent.GetPointerId(actionIndex);

        switch (action)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                float dipX = motionEvent.GetX(actionIndex) / density;
                float dipY = motionEvent.GetY(actionIndex) / density;
                ProcessInitialTouchDown(new PointF(dipX, dipY), pointerId);
                e.Handled = true;
                break;

            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
            case MotionEventActions.Cancel:
                CancelParentCornerHoldIfMatchingPointer(pointerId);
                e.Handled = true;
                break;

            case MotionEventActions.Move:
                if (_cornerHoldPointerId.HasValue)
                {
                    int holdIndex = motionEvent.FindPointerIndex(_cornerHoldPointerId.Value);
                    if (holdIndex >= 0)
                    {
                        var movePoint = new PointF(
                            motionEvent.GetX(holdIndex) / density,
                            motionEvent.GetY(holdIndex) / density);
                        RectF cornerZone = TappyGameDrawable.GetParentCornerZone((float)_graphicsView.Width);
                        if (!cornerZone.Contains(movePoint))
                        {
                            CancelParentCornerHoldIfMatchingPointer(_cornerHoldPointerId.Value);
                        }
                    }
                }
                e.Handled = true;
                break;
        }
    }
#endif

    private void OnGraphicsViewStartInteraction(object? sender, TouchEventArgs e)
    {
#if ANDROID
        if (_useAndroidNativeMultiTouch)
        {
            return;
        }
#endif
        if (e.Touches is { Length: > 0 })
        {
            ProcessInitialTouchDown(e.Touches[0], pointerId: 0);
        }
    }

    private void OnGraphicsViewEndInteraction(object? sender, TouchEventArgs e)
    {
#if ANDROID
        if (_useAndroidNativeMultiTouch)
        {
            return;
        }
#endif
        CancelParentCornerHoldIfMatchingPointer(pointerId: 0);
    }

    private void OnGraphicsViewCancelInteraction(object? sender, EventArgs e)
    {
        if (_cornerHoldPointerId.HasValue)
        {
            CancelParentCornerHoldIfMatchingPointer(_cornerHoldPointerId.Value);
        }
    }

    private void ProcessInitialTouchDown(PointF touchPoint, int pointerId)
    {
        float canvasWidth = (float)_graphicsView.Width;
        float canvasHeight = (float)_graphicsView.Height;
        if (canvasWidth <= 0f || canvasHeight <= 0f)
        {
            return;
        }

        if (_parentSheet is not null)
        {
            HandleParentSheetTouch(touchPoint);
            return;
        }

        RectF cornerZone = TappyGameDrawable.GetParentCornerZone(canvasWidth);
        if (cornerZone.Contains(touchPoint))
        {
            if (!_deviceLockService.IsTaskLocked)
            {
                OpenParentSheet(alreadyUnlocked: true);
                return;
            }

            _cornerHoldPointerId = pointerId;
            _cornerHoldStartTickMs = Environment.TickCount64;
            _drawable.CornerHoldProgress = 0.04f;
            _hapticService.TriggerGentleTapHaptic();
            return;
        }

        switch (_currentMode)
        {
            case TappyGameMode.ShapePop:
                HandleShapePopTouchDown(touchPoint);
                break;

            case TappyGameMode.FloatingBubbles:
                HandleFloatingBubblesTouchDown(touchPoint);
                break;

            case TappyGameMode.StarlightFireworks:
                HandleStarlightFireworksTouchDown(touchPoint);
                break;

            case TappyGameMode.RainbowKeys:
                HandleRainbowKeysTouchDown(touchPoint);
                break;
        }
    }

    private void HandleShapePopTouchDown(PointF touchPoint)
    {
        if (_currentShape is not null && _currentShape.ContainsPoint(touchPoint))
        {
            TappyShape popped = _currentShape;
            _scorePopCount++;
            _drawable.ScorePopCount = _scorePopCount;

            _hapticService.TriggerShapePopHaptic();
            _audioService.PlayPopSound(popped.NoteIndex);

            EmitCelebrationBurst(popped.Center, popped.FillColor, popped.Radius);
            SpawnNextShape(previousCenter: popped.Center);
        }
        else
        {
            _particles.AddRing(touchPoint, Palette.White.WithAlpha(0.45f), 8f, 44f, 0.28f, 3f);
        }
    }

    private void HandleFloatingBubblesTouchDown(PointF touchPoint)
    {
        bool hitAny = false;
        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            FloatingBubble bubble = _bubbles[i];
            if (bubble.ContainsPoint(touchPoint))
            {
                hitAny = true;
                _scorePopCount++;
                _drawable.ScorePopCount = _scorePopCount;

                _hapticService.TriggerShapePopHaptic();
                _audioService.PlayBubblePop(bubble.NoteIndex);

                EmitBubblePopBurst(bubble.Center, bubble.RimColor, bubble.Radius);
                _bubbles[i] = CreateRandomBubble(spawnAtBottom: true);
                break;
            }
        }

        if (!hitAny)
        {
            _particles.AddRing(touchPoint, Palette.Sky.WithAlpha(0.5f), 10f, 52f, 0.32f, 3f);
        }
    }

    private void HandleStarlightFireworksTouchDown(PointF touchPoint)
    {
        _scorePopCount++;
        _drawable.ScorePopCount = _scorePopCount;

        int noteIdx = Interlocked.Increment(ref _rollingNoteIndex);
        _hapticService.TriggerShapePopHaptic();
        _audioService.PlaySparkleSound(noteIdx);

        Color primary = Palette.Pick(_rng);
        Color secondary = Palette.Pick(_rng, avoid: primary);

        _particles.AddRing(touchPoint, primary, 14f, 155f, 0.48f, 7f);
        _particles.AddRing(touchPoint, Palette.White, 8f, 95f, 0.34f, 4f);

        _particles.Burst(
            touchPoint,
            count: 26,
            colors: [primary, secondary, Palette.Yellow, Palette.White],
            speedMin: 110f,
            speedMax: 360f,
            sizeMin: 8f,
            sizeMax: 16f,
            lifeMin: 0.45f,
            lifeMax: 0.85f,
            kind: ParticleKind.Star,
            gravity: 110f,
            drag: 2.1f,
            spawnRadius: 12f);

        _particles.Burst(
            touchPoint,
            count: 22,
            colors: [primary, Palette.White],
            speedMin: 160f,
            speedMax: 440f,
            sizeMin: 3.5f,
            sizeMax: 6f,
            lifeMin: 0.30f,
            lifeMax: 0.55f,
            kind: ParticleKind.Spark,
            gravity: 60f,
            drag: 2.8f,
            spawnRadius: 6f);
    }

    private void HandleRainbowKeysTouchDown(PointF touchPoint)
    {
        for (int i = 0; i < _rainbowKeys.Count; i++)
        {
            RainbowKeyBar key = _rainbowKeys[i];
            if (key.Bounds.Contains(touchPoint))
            {
                _scorePopCount++;
                _drawable.ScorePopCount = _scorePopCount;

                key.PressBounce = 1f;
                key.GlowAlpha = 1f;

                _hapticService.TriggerShapePopHaptic();
                _audioService.PlayXylophoneNote(key.KeyIndex);

                _particles.AddRing(touchPoint, Palette.White, 16f, 110f, 0.35f, 5f);
                _particles.Burst(
                    touchPoint,
                    count: 18,
                    colors: [key.BaseColor, Palette.White, Palette.Yellow],
                    speedMin: 90f,
                    speedMax: 280f,
                    sizeMin: 7f,
                    sizeMax: 14f,
                    lifeMin: 0.35f,
                    lifeMax: 0.65f,
                    kind: ParticleKind.Star,
                    gravity: 95f,
                    drag: 2.2f,
                    spawnRadius: 14f);
                break;
            }
        }
    }

    private void EmitCelebrationBurst(PointF center, Color color, float radius)
    {
        Color accent = Palette.Lighten(color, 0.45f);
        _particles.AddRing(center, color, radius * 0.6f, radius * 1.85f, 0.38f, 9f);
        _particles.AddRing(center, Palette.White, radius * 0.35f, radius * 1.35f, 0.26f, 5f);

        _particles.Burst(
            center,
            count: 24,
            colors: [color, accent, Palette.White, Palette.Yellow],
            speedMin: 130f,
            speedMax: 380f,
            sizeMin: 7f,
            sizeMax: 14f,
            lifeMin: 0.35f,
            lifeMax: 0.68f,
            kind: ParticleKind.Star,
            gravity: 140f,
            drag: 2.4f,
            spawnRadius: radius * 0.35f);

        _particles.Burst(
            center,
            count: 18,
            colors: [color, Palette.White],
            speedMin: 160f,
            speedMax: 420f,
            sizeMin: 4f,
            sizeMax: 8f,
            lifeMin: 0.25f,
            lifeMax: 0.48f,
            kind: ParticleKind.Dot,
            gravity: 80f,
            drag: 2.6f,
            spawnRadius: radius * 0.25f);
    }

    private void EmitBubblePopBurst(PointF center, Color color, float radius)
    {
        _particles.AddRing(center, Palette.Lighten(color, 0.4f), radius * 0.8f, radius * 1.65f, 0.32f, 5f);
        _particles.Burst(
            center,
            count: 20,
            colors: [color, Palette.White, Palette.Sky],
            speedMin: 90f,
            speedMax: 290f,
            sizeMin: 4f,
            sizeMax: 9f,
            lifeMin: 0.28f,
            lifeMax: 0.55f,
            kind: ParticleKind.Dot,
            gravity: -40f,
            drag: 2.5f,
            spawnRadius: radius * 0.5f);
    }

    private void SpawnNextShape(PointF? previousCenter)
    {
        float diameter = _rng.Next((int)TappyShape.MinDiameterUnits, (int)TappyShape.MaxDiameterUnits + 1);
        float radius = diameter * 0.5f;

        RectF safeBounds = GetSafeSpawnCenterBounds(radius);
        RectF parentCorner = TappyGameDrawable.GetParentCornerZone((float)_graphicsView.Width);

        PointF candidateCenter = safeBounds.Center;
        float minSeparationDistance = Math.Max(diameter * 1.15f, 160f);

        for (int attempt = 0; attempt < 25; attempt++)
        {
            float x = safeBounds.Left + ((float)_rng.NextDouble() * Math.Max(1f, safeBounds.Width));
            float y = safeBounds.Top + ((float)_rng.NextDouble() * Math.Max(1f, safeBounds.Height));
            candidateCenter = new PointF(x, y);

            if (candidateCenter.Y < (radius + 64f) &&
                (candidateCenter.X > parentCorner.Left - radius || candidateCenter.X < 240f + radius))
            {
                continue;
            }

            if (previousCenter is null || Distance(candidateCenter, previousCenter.Value) >= minSeparationDistance)
            {
                break;
            }

            if (attempt == 15)
            {
                minSeparationDistance *= 0.65f;
            }
        }

        Color nextColor = Palette.Pick(_rng, avoid: _lastShapeColor);
        _lastShapeColor = nextColor;
        ShapeKind kind = ShapePainter.AllKinds[_rng.Next(ShapePainter.AllKinds.Length)];
        int noteIdx = Interlocked.Increment(ref _rollingNoteIndex);

        _currentShape = new TappyShape
        {
            Center = candidateCenter,
            Diameter = diameter,
            FillColor = nextColor,
            Kind = kind,
            NoteIndex = noteIdx,
            SpawnAgeSeconds = 0f,
            BreathPhase = (float)(_rng.NextDouble() * MathF.Tau),
            BlinkCountdown = 1.8f + ((float)_rng.NextDouble() * 2.4f)
        };

        _drawable.ActiveShape = _currentShape;
    }

    private void PopulateInitialBubbles()
    {
        _bubbles.Clear();
        for (int i = 0; i < MaxFloatingBubbles; i++)
        {
            _bubbles.Add(CreateRandomBubble(spawnAtBottom: false));
        }
    }

    private FloatingBubble CreateRandomBubble(bool spawnAtBottom)
    {
        float width = Math.Max(360f, (float)_graphicsView.Width);
        float height = Math.Max(260f, (float)_graphicsView.Height);
        float radius = 58f + ((float)_rng.NextDouble() * 24f);

        float x = radius + 24f + ((float)_rng.NextDouble() * Math.Max(20f, width - ((radius + 24f) * 2f)));
        float y = spawnAtBottom
            ? height + radius + ((float)_rng.NextDouble() * 60f)
            : radius + 70f + ((float)_rng.NextDouble() * Math.Max(20f, height - (radius * 2f) - 90f));

        return new FloatingBubble
        {
            Center = new PointF(x, y),
            Radius = radius,
            RiseSpeed = 42f + ((float)_rng.NextDouble() * 38f),
            SwayAmplitude = 18f + ((float)_rng.NextDouble() * 22f),
            SwayFrequency = 1.1f + ((float)_rng.NextDouble() * 1.2f),
            Phase = (float)(_rng.NextDouble() * MathF.Tau),
            SpawnAge = 0f,
            RimColor = Palette.Pick(_rng),
            InnerBuddyKind = ShapePainter.AllKinds[_rng.Next(ShapePainter.AllKinds.Length)],
            NoteIndex = Interlocked.Increment(ref _rollingNoteIndex)
        };
    }

    private RectF GetSafeSpawnCenterBounds(float shapeRadius)
    {
        float margin = shapeRadius + 24f;
        float topMargin = shapeRadius + 66f;
        float width = (float)_graphicsView.Width;
        float height = (float)_graphicsView.Height;

        float minX = Math.Min(margin, width * 0.5f);
        float maxX = Math.Max(minX + 1f, width - margin);
        float minY = Math.Min(topMargin, height * 0.5f);
        float maxY = Math.Max(minY + 1f, height - margin);

        return new RectF(minX, minY, maxX - minX, maxY - minY);
    }

    private void CancelParentCornerHoldIfMatchingPointer(int pointerId)
    {
        if (_cornerHoldPointerId == pointerId)
        {
            _cornerHoldPointerId = null;
            _drawable.CornerHoldProgress = 0f;
        }
    }

    private void OpenParentSheet(bool alreadyUnlocked)
    {
        _cornerHoldPointerId = null;
        _drawable.CornerHoldProgress = 0f;
        _hapticService.TriggerParentGateSuccessHaptic();
        _audioService.PlayUiTap();

        _parentSheet = new ParentControlSheet
        {
            IsGateUnlocked = alreadyUnlocked,
            GateNextStep = alreadyUnlocked ? 4 : 1,
            IdleSeconds = 0f
        };
        _drawable.ParentSheet = _parentSheet;
        SyncDrawableFlags();
    }

    private void CloseParentSheetAndStartBabyPlay(bool ensurePinned)
    {
        _parentSheet = null;
        _drawable.ParentSheet = null;
        _drawable.CornerHoldProgress = 0f;

        if (ensurePinned)
        {
#if ANDROID
            try
            {
                Platform.CurrentActivity?.StartLockTask();
            }
            catch
            {
                // Handled by DeviceLockService
            }
#endif
            _deviceLockService.StartKioskLockTask();
        }

        _deviceLockService.EnforceImmersiveFullscreen();
        SyncDrawableFlags();
    }

    private void HandleParentSheetTouch(PointF touchPoint)
    {
        if (_parentSheet is null)
        {
            return;
        }

        _parentSheet.IdleSeconds = 0f;

        if (_parentSheet.ResumePlayButtonRect.Contains(touchPoint))
        {
            _audioService.PlayUiTap();
            _hapticService.TriggerGentleTapHaptic();
            CloseParentSheetAndStartBabyPlay(ensurePinned: true);
            return;
        }

        if (!_parentSheet.IsGateUnlocked)
        {
            for (int i = 0; i < _parentSheet.GateStepRects.Length; i++)
            {
                if (_parentSheet.GateStepRects[i].Contains(touchPoint))
                {
                    int tappedStep = i + 1;
                    if (tappedStep == _parentSheet.GateNextStep)
                    {
                        _parentSheet.GateNextStep++;
                        _hapticService.TriggerGentleTapHaptic();
                        _audioService.PlayXylophoneNote(i * 2);

                        if (_parentSheet.GateNextStep > 3)
                        {
                            _parentSheet.IsGateUnlocked = true;
                            _hapticService.TriggerParentGateSuccessHaptic();
                            _audioService.PlayUnlockChime();
                            UnpinScreenNow();
                        }
                    }
                    else if (tappedStep > _parentSheet.GateNextStep)
                    {
                        _parentSheet.GateNextStep = 1;
                    }
                    return;
                }
            }
            return;
        }

        for (int i = 0; i < _parentSheet.ModeCardRects.Length; i++)
        {
            if (_parentSheet.ModeCardRects[i].Contains(touchPoint))
            {
                SelectGameMode(GameModeCatalog.All[i].Mode);
                _audioService.PlayUnlockChime();
                _hapticService.TriggerGentleTapHaptic();
                CloseParentSheetAndStartBabyPlay(ensurePinned: true);
                return;
            }
        }

        if (_parentSheet.SoundToggleRect.Contains(touchPoint))
        {
            _audioService.IsMuted = !_audioService.IsMuted;
            if (!_audioService.IsMuted)
            {
                _audioService.PlayUiTap();
            }
            _hapticService.TriggerGentleTapHaptic();
            SyncDrawableFlags();
            return;
        }

        if (_parentSheet.HapticToggleRect.Contains(touchPoint))
        {
            _hapticService.IsEnabled = !_hapticService.IsEnabled;
            _hapticService.TriggerGentleTapHaptic();
            _audioService.PlayUiTap();
            SyncDrawableFlags();
            return;
        }

        if (_parentSheet.UnpinButtonRect.Contains(touchPoint))
        {
            _hapticService.TriggerParentGateSuccessHaptic();
            _audioService.PlayUnlockChime();
            if (_deviceLockService.IsTaskLocked)
            {
                UnpinScreenNow();
            }
            else
            {
                _deviceLockService.StartKioskLockTask();
            }
            SyncDrawableFlags();
            return;
        }

        if (_parentSheet.ExitAppButtonRect.Contains(touchPoint))
        {
            _hapticService.TriggerParentGateSuccessHaptic();
            UnpinScreenNow();
            _deviceLockService.ExitApplication();
        }
    }

    private void UnpinScreenNow()
    {
#if ANDROID
        try
        {
            Platform.CurrentActivity?.StopLockTask();
        }
        catch
        {
            // Handled by service
        }
#endif
        _deviceLockService.StopKioskLockTask();
        SyncDrawableFlags();
    }

    private void SelectGameMode(TappyGameMode mode)
    {
        _currentMode = mode;
        _drawable.CurrentMode = mode;
        _particles.Clear();

        switch (mode)
        {
            case TappyGameMode.ShapePop:
                SpawnNextShape(previousCenter: null);
                break;

            case TappyGameMode.FloatingBubbles:
                PopulateInitialBubbles();
                break;

            case TappyGameMode.RainbowKeys:
                LayoutRainbowKeys();
                break;
        }
    }

    private void SyncDrawableFlags()
    {
        _drawable.IsTaskLocked = _deviceLockService.IsTaskLocked;
        _drawable.IsAudioMuted = _audioService.IsMuted;
        _drawable.IsHapticsEnabled = _hapticService.IsEnabled;
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        long nowMs = Environment.TickCount64;
        float dt = Math.Clamp((nowMs - _lastFrameTickMs) / 1000f, 0.001f, 0.05f);
        _lastFrameTickMs = nowMs;

        _drawable.AmbientTime += dt;

        if (_currentMode == TappyGameMode.ShapePop && _currentShape is not null)
        {
            _currentShape.SpawnAgeSeconds += dt;
            _currentShape.BreathPhase += dt * 3.1f;

            if (_currentShape.BlinkDurationRemaining > 0f)
            {
                _currentShape.BlinkDurationRemaining = Math.Max(0f, _currentShape.BlinkDurationRemaining - dt);
            }
            else
            {
                _currentShape.BlinkCountdown -= dt;
                if (_currentShape.BlinkCountdown <= 0f)
                {
                    _currentShape.BlinkDurationRemaining = 0.14f;
                    _currentShape.BlinkCountdown = 2.0f + ((float)_rng.NextDouble() * 2.8f);
                }
            }
        }
        else if (_currentMode == TappyGameMode.FloatingBubbles)
        {
            float canvasW = (float)_graphicsView.Width;
            for (int i = 0; i < _bubbles.Count; i++)
            {
                FloatingBubble b = _bubbles[i];
                b.SpawnAge += dt;
                b.Phase += dt * b.SwayFrequency;
                float newY = b.Center.Y - (b.RiseSpeed * dt);
                float newX = Math.Clamp(
                    b.Center.X + (MathF.Cos(b.Phase) * b.SwayAmplitude * dt),
                    b.Radius + 16f,
                    Math.Max(b.Radius + 20f, canvasW - b.Radius - 16f));

                b.Center = new PointF(newX, newY);

                if (b.Center.Y < -b.Radius - 20f)
                {
                    _bubbles[i] = CreateRandomBubble(spawnAtBottom: true);
                }
            }
        }
        else if (_currentMode == TappyGameMode.RainbowKeys)
        {
            for (int i = 0; i < _rainbowKeys.Count; i++)
            {
                RainbowKeyBar key = _rainbowKeys[i];
                key.PressBounce = Ease.Damp(key.PressBounce, 0f, 14f, dt);
                key.GlowAlpha = Ease.Damp(key.GlowAlpha, 0f, 8f, dt);
            }
        }

        _particles.Update(dt);

        if (_cornerHoldPointerId.HasValue)
        {
            long elapsedHold = nowMs - _cornerHoldStartTickMs;
            float holdProgress = Ease.Clamp01((float)elapsedHold / ParentCornerHoldDurationMs);
            _drawable.CornerHoldProgress = holdProgress;

            if (holdProgress >= 1f)
            {
                OpenParentSheet(alreadyUnlocked: false);
            }
        }

        if (_parentSheet is { IsGateUnlocked: false })
        {
            _parentSheet.IdleSeconds += dt;
            if (_parentSheet.IdleSeconds >= ParentControlSheet.AutoCloseTimeoutSeconds)
            {
                _parentSheet = null;
                _drawable.ParentSheet = null;
            }
        }

        _graphicsView.Invalidate();
    }

    private static float Distance(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
