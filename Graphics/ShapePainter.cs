namespace Tappy.Graphics;

public enum ShapeKind
{
    Circle,
    Square,
    Triangle,
    Star,
    Heart,
    Hexagon
}

/// <summary>
/// Draws soft, rounded "friendly" shapes with optional cartoon faces.
/// Unit paths are built once (radius = 1) and scaled at draw time, so no per-frame geometry allocations.
/// </summary>
public static class ShapePainter
{
    public static readonly ShapeKind[] AllKinds = Enum.GetValues<ShapeKind>();

    private static readonly PathF SquarePath = RoundedPolygon(4, 1.3f, MathF.PI / 4f, 0.24f);
    private static readonly PathF TrianglePath = RoundedPolygon(3, 1.22f, -MathF.PI / 2f, 0.2f);
    private static readonly PathF HexagonPath = RoundedPolygon(6, 1.06f, 0f, 0.18f);
    private static readonly PathF StarPath = RoundedStar(5, 1.14f, 0.58f, 0.2f);
    private static readonly PathF HeartPath = BuildHeart();

    public static PathF UnitStar => StarPath;

    public static void DrawFriendly(
        ICanvas canvas,
        ShapeKind kind,
        float cx,
        float cy,
        float radius,
        Color fill,
        float rotationDegrees = 0f,
        float scaleX = 1f,
        float scaleY = 1f,
        float blink = 1f,
        float alpha = 1f,
        bool face = true,
        Color? faceInk = null)
    {
        if (radius <= 0.5f || alpha <= 0.001f)
        {
            return;
        }

        canvas.SaveState();
        canvas.Translate(cx, cy);
        if (rotationDegrees != 0f)
        {
            canvas.Rotate(rotationDegrees);
        }

        float rx = radius * scaleX;
        float ry = radius * scaleY;

        canvas.Alpha = alpha;
        canvas.FillColor = fill;
        FillUnitShape(canvas, kind, rx, ry);

        // Soft specular shine
        (float shineX, float shineY) = kind switch
        {
            ShapeKind.Triangle => (-0.2f, -0.18f),
            ShapeKind.Star => (-0.22f, -0.26f),
            ShapeKind.Heart => (-0.5f, -0.45f),
            _ => (-0.44f, -0.44f)
        };
        canvas.FillColor = Colors.White;
        canvas.Alpha = alpha * 0.45f;
        canvas.FillCircle(shineX * rx, shineY * ry, radius * 0.1f);
        canvas.Alpha = alpha * 0.3f;
        canvas.FillCircle((shineX + 0.17f) * rx, (shineY - 0.1f) * ry, radius * 0.05f);

        if (face)
        {
            DrawFace(canvas, kind, radius, scaleX, scaleY, blink, faceInk ?? Palette.FaceInk, alpha);
        }

        canvas.RestoreState();
    }

    private static void FillUnitShape(ICanvas canvas, ShapeKind kind, float rx, float ry)
    {
        if (kind == ShapeKind.Circle)
        {
            canvas.FillEllipse(-rx, -ry, rx * 2f, ry * 2f);
            return;
        }

        canvas.SaveState();
        canvas.Scale(rx, ry);
        canvas.FillPath(kind switch
        {
            ShapeKind.Square => SquarePath,
            ShapeKind.Triangle => TrianglePath,
            ShapeKind.Star => StarPath,
            ShapeKind.Heart => HeartPath,
            _ => HexagonPath
        });
        canvas.RestoreState();
    }

    private static void DrawFace(ICanvas canvas, ShapeKind kind, float r, float sx, float sy, float blink, Color ink, float alpha)
    {
        (float offsetY, float faceScale) = kind switch
        {
            ShapeKind.Triangle => (0.24f, 0.74f),
            ShapeKind.Star => (0.06f, 0.7f),
            ShapeKind.Heart => (-0.1f, 0.85f),
            _ => (0.02f, 1f)
        };

        float s = r * faceScale;
        float cy = offsetY * r * sy;
        float eyeDx = 0.3f * s * sx;
        float eyeY = cy - (0.1f * s * sy);
        float eyeW = 0.17f * s;
        float eyeH = 0.23f * s * MathF.Max(0.12f, blink);

        // Eyes
        canvas.Alpha = alpha;
        canvas.FillColor = ink;
        canvas.FillEllipse(-eyeDx - (eyeW / 2f), eyeY - (eyeH / 2f), eyeW, eyeH);
        canvas.FillEllipse(eyeDx - (eyeW / 2f), eyeY - (eyeH / 2f), eyeW, eyeH);

        if (blink > 0.6f)
        {
            canvas.FillColor = Colors.White;
            canvas.FillCircle(-eyeDx + (eyeW * 0.2f), eyeY - (eyeH * 0.2f), eyeW * 0.22f);
            canvas.FillCircle(eyeDx + (eyeW * 0.2f), eyeY - (eyeH * 0.2f), eyeW * 0.22f);
        }

        // Cheeks
        canvas.FillColor = Palette.Blush;
        canvas.Alpha = alpha * 0.55f;
        float cheekW = 0.2f * s;
        float cheekH = 0.11f * s;
        float cheekY = cy + (0.1f * s) - (cheekH / 2f);
        canvas.FillEllipse((-0.5f * s * sx) - (cheekW / 2f), cheekY, cheekW, cheekH);
        canvas.FillEllipse((0.5f * s * sx) - (cheekW / 2f), cheekY, cheekW, cheekH);

        // Smile
        canvas.Alpha = alpha;
        canvas.StrokeColor = ink;
        canvas.StrokeSize = MathF.Max(2f, 0.07f * s);
        canvas.StrokeLineCap = LineCap.Round;
        var smile = new PathF();
        smile.MoveTo(-0.17f * s, cy + (0.1f * s));
        smile.QuadTo(0f, cy + (0.3f * s), 0.17f * s, cy + (0.1f * s));
        canvas.DrawPath(smile);
    }

    public static PathF RoundedPolygon(int sides, float radius, float rotation, float corner)
    {
        var vertices = new PointF[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = rotation + (i * MathF.Tau / sides);
            vertices[i] = new PointF(MathF.Cos(a) * radius, MathF.Sin(a) * radius);
        }

        return BuildRounded(vertices, corner);
    }

    public static PathF RoundedStar(int points, float outer, float inner, float corner)
    {
        var vertices = new PointF[points * 2];
        for (int i = 0; i < vertices.Length; i++)
        {
            float a = (-MathF.PI / 2f) + (i * MathF.PI / points);
            float r = (i % 2 == 0) ? outer : inner;
            vertices[i] = new PointF(MathF.Cos(a) * r, MathF.Sin(a) * r);
        }

        return BuildRounded(vertices, corner);
    }

    private static PathF BuildRounded(PointF[] v, float corner)
    {
        var path = new PathF();
        int n = v.Length;
        for (int i = 0; i < n; i++)
        {
            PointF prev = v[(i - 1 + n) % n];
            PointF cur = v[i];
            PointF next = v[(i + 1) % n];
            float p1x = cur.X + ((prev.X - cur.X) * corner);
            float p1y = cur.Y + ((prev.Y - cur.Y) * corner);
            float p2x = cur.X + ((next.X - cur.X) * corner);
            float p2y = cur.Y + ((next.Y - cur.Y) * corner);

            if (i == 0)
            {
                path.MoveTo(p1x, p1y);
            }
            else
            {
                path.LineTo(p1x, p1y);
            }

            path.QuadTo(cur.X, cur.Y, p2x, p2y);
        }

        path.Close();
        return path;
    }

    private static PathF BuildHeart()
    {
        var path = new PathF();
        path.MoveTo(0f, -0.45f);
        path.CurveTo(0f, -0.92f, 0.86f, -1.06f, 0.99f, -0.4f);
        path.CurveTo(1.08f, 0.1f, 0.45f, 0.56f, 0f, 0.96f);
        path.CurveTo(-0.45f, 0.56f, -1.08f, 0.1f, -0.99f, -0.4f);
        path.CurveTo(-0.86f, -1.06f, 0f, -0.92f, 0f, -0.45f);
        path.Close();
        return path;
    }
}

