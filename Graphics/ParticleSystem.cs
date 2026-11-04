namespace Tappy.Graphics;

public enum ParticleKind : byte
{
    Dot,
    Spark,
    Star
}

/// <summary>
/// Fixed-capacity, struct-based particle + ring system. Zero allocations per frame,
/// so chaotic multi-finger smushes never trigger GC hitches.
/// </summary>
public sealed class ParticleSystem
{
    private struct Particle
    {
        public float X, Y, VX, VY, Age, Life, Size, Gravity, Drag, Rotation, Spin, Alpha;
        public Color Color;
        public ParticleKind Kind;
    }

    private struct Ring
    {
        public float X, Y, StartRadius, EndRadius, Age, Life, Width;
        public Color Color;
    }

    private readonly Particle[] _particles;
    private readonly Ring[] _rings;
    private readonly Random _rng;
    private int _particleCount;
    private int _ringCount;

    public ParticleSystem(Random rng, int capacity = 700, int ringCapacity = 48)
    {
        _rng = rng;
        _particles = new Particle[capacity];
        _rings = new Ring[ringCapacity];
    }

    public void Clear()
    {
        _particleCount = 0;
        _ringCount = 0;
    }

    public void Emit(
        float x, float y, float vx, float vy, float life, float size, Color color, ParticleKind kind,
        float gravity = 0f, float drag = 0f, float alpha = 1f)
    {
        int index = _particleCount < _particles.Length ? _particleCount++ : _rng.Next(_particles.Length);
        _particles[index] = new Particle
        {
            X = x,
            Y = y,
            VX = vx,
            VY = vy,
            Life = MathF.Max(0.05f, life),
            Size = size,
            Gravity = gravity,
            Drag = drag,
            Rotation = (float)(_rng.NextDouble() * 360.0),
            Spin = ((float)_rng.NextDouble() - 0.5f) * 540f,
            Alpha = alpha,
            Color = color,
            Kind = kind
        };
    }

    public void Burst(
        PointF center, int count, Color[] colors,
        float speedMin, float speedMax, float sizeMin, float sizeMax, float lifeMin, float lifeMax,
        ParticleKind kind, float gravity = 0f, float drag = 1.5f, float spawnRadius = 0f, float alpha = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * MathF.Tau);
            float dx = MathF.Cos(angle);
            float dy = MathF.Sin(angle);
            float speed = Rand(speedMin, speedMax);
            float offset = spawnRadius * (float)_rng.NextDouble();
            Emit(
                center.X + (dx * offset),
                center.Y + (dy * offset),
                dx * speed,
                dy * speed,
                Rand(lifeMin, lifeMax),
                Rand(sizeMin, sizeMax),
                colors[i % colors.Length],
                kind,
                gravity,
                drag,
                alpha);
        }
    }

    public void AddRing(PointF center, Color color, float startRadius, float endRadius, float life, float width)
    {
        int index = _ringCount < _rings.Length ? _ringCount++ : _rng.Next(_rings.Length);
        _rings[index] = new Ring
        {
            X = center.X,
            Y = center.Y,
            StartRadius = startRadius,
            EndRadius = endRadius,
            Life = MathF.Max(0.05f, life),
            Width = width,
            Color = color
        };
    }

    public void Update(float dt)
    {
        for (int i = 0; i < _particleCount;)
        {
            ref Particle p = ref _particles[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _particles[i] = _particles[--_particleCount];
                continue;
            }

            float dragFactor = MathF.Exp(-p.Drag * dt);
            p.VX *= dragFactor;
            p.VY = (p.VY * dragFactor) + (p.Gravity * dt);
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Rotation += p.Spin * dt;
            i++;
        }

        for (int i = 0; i < _ringCount;)
        {
            ref Ring r = ref _rings[i];
            r.Age += dt;
            if (r.Age >= r.Life)
            {
                _rings[i] = _rings[--_ringCount];
                continue;
            }

            i++;
        }
    }

    public void Draw(ICanvas canvas)
    {
        if (_particleCount == 0 && _ringCount == 0)
        {
            return;
        }

        canvas.SaveState();
        canvas.StrokeLineCap = LineCap.Round;

        for (int i = 0; i < _ringCount; i++)
        {
            ref Ring r = ref _rings[i];
            float t = r.Age / r.Life;
            float radius = Ease.Lerp(r.StartRadius, r.EndRadius, Ease.OutCubic(t));
            float fade = 1f - t;
            canvas.Alpha = fade * fade;
            canvas.StrokeColor = r.Color;
            canvas.StrokeSize = MathF.Max(1f, r.Width * (1f - (0.6f * t)));
            canvas.DrawCircle(r.X, r.Y, radius);
        }

        for (int i = 0; i < _particleCount; i++)
        {
            ref Particle p = ref _particles[i];
            float t = p.Age / p.Life;
            float alpha = p.Alpha * (1f - (t * t));
            if (alpha <= 0.01f)
            {
                continue;
            }

            float size = p.Size * (1f - (0.5f * t));
            canvas.Alpha = alpha;

            switch (p.Kind)
            {
                case ParticleKind.Dot:
                    canvas.FillColor = p.Color;
                    canvas.FillCircle(p.X, p.Y, size);
                    break;

                case ParticleKind.Spark:
                    canvas.StrokeColor = p.Color;
                    canvas.StrokeSize = size;
                    canvas.DrawLine(p.X - (p.VX * 0.045f), p.Y - (p.VY * 0.045f), p.X, p.Y);
                    break;

                case ParticleKind.Star:
                    canvas.SaveState();
                    canvas.Translate(p.X, p.Y);
                    canvas.Rotate(p.Rotation);
                    canvas.Scale(size, size);
                    canvas.FillColor = p.Color;
                    canvas.FillPath(ShapePainter.UnitStar);
                    canvas.RestoreState();
                    break;
            }
        }

        canvas.Alpha = 1f;
        canvas.RestoreState();
    }

    private float Rand(float min, float max) => min + ((float)_rng.NextDouble() * (max - min));
}

