using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>沿倾斜圆柱卷轴切分纸面；平坦部分各用一个多边形，弯曲部分用连续的仿射条带。</summary>
internal sealed class PageCurlGeometry
{
    public Vector2 Normal { get; }
    public float Crease { get; }
    public float Radius { get; }
    public float Progress { get; }
    private readonly Vector2[] corners;
    private readonly float minProjection;
    private readonly float maxProjection;

    public PageCurlGeometry(Rect bounds, float amount, bool fromRight, float tilt)
    {
        float width = (float)bounds.Width;
        Progress = width > 0 ? PageTurnService.ClampCurlAmount(amount, width) / (width * 2) : 0;
        // 接近完成时逐渐放平，使背面准确落到下一双页的书脊和边界。
        float settle = Math.Clamp((1 - Progress) / 0.35f, 0, 1);
        tilt *= settle * settle * (3 - 2 * settle);
        Normal = new Vector2((fromRight ? 1 : -1) * MathF.Cos(tilt), MathF.Sin(tilt));
        corners = RectangleCorners(bounds);
        minProjection = corners.Min(p => Vector2.Dot(p, Normal));
        maxProjection = corners.Max(p => Vector2.Dot(p, Normal));
        float span = maxProjection - minProjection;
        float travel = Progress * 2 * span;
        Radius = Math.Max(0, Math.Min(40, Math.Min(travel, span * 2 - travel) / MathF.PI));
        Crease = maxProjection - travel / 2 - MathF.PI * Radius / 2;
    }

    public IEnumerable<PageCurlStrip> GetStrips()
    {
        // 按离卷起外沿由远及近的顺序绘制，背面覆盖正面。
        yield return MakeStrip(minProjection, Crease, 1, 0, false, 1);
        const int segments = 40; // 圆柱前后各 20 段，避免跨过法线为零的轮廓线。
        for (int i = 0; i < segments && Radius > 0; i++)
        {
            float a = MathF.PI * i / segments;
            float b = MathF.PI * (i + 1) / segments;
            float start = Crease + a * Radius;
            float end = Crease + b * Radius;
            float mappedStart = Crease + MathF.Sin(a) * Radius;
            float mappedEnd = Crease + MathF.Sin(b) * Radius;
            float scale = (mappedEnd - mappedStart) / (end - start);
            yield return MakeStrip(start, end, scale, mappedStart - scale * start,
                i >= segments / 2, 1 - 0.18f * MathF.Sin((a + b) / 2));
        }
        yield return MakeStrip(Crease + MathF.PI * Radius, maxProjection, -1,
            2 * Crease + MathF.PI * Radius, true, 0.9f + 0.1f * Progress);
    }

    private PageCurlStrip MakeStrip(float start, float end, float scale, float offset, bool isBack, float shade)
    {
        var polygon = end > start ? ClipBand(corners, Normal, start, end) : Array.Empty<Vector2>();
        float k = scale - 1;
        var transform = new Matrix3x2(1 + k * Normal.X * Normal.X, k * Normal.X * Normal.Y,
            k * Normal.X * Normal.Y, 1 + k * Normal.Y * Normal.Y, offset * Normal.X, offset * Normal.Y);
        return new PageCurlStrip(polygon, transform, isBack, shade);
    }

    public static Vector2[] RectangleCorners(Rect bounds) =>
    [
        new((float)bounds.X, (float)bounds.Y),
        new((float)(bounds.X + bounds.Width), (float)bounds.Y),
        new((float)(bounds.X + bounds.Width), (float)(bounds.Y + bounds.Height)),
        new((float)bounds.X, (float)(bounds.Y + bounds.Height))
    ];

    public static Vector2[] ClipBand(Vector2[] polygon, Vector2 normal, float start, float end) =>
        Clip(Clip(polygon, normal, start, true), normal, end, false);

    private static Vector2[] Clip(Vector2[] polygon, Vector2 normal, float limit, bool keepAbove)
    {
        if (polygon.Length == 0) return polygon;
        var result = new List<Vector2>(6);
        Vector2 previous = polygon[^1];
        float previousDistance = Vector2.Dot(previous, normal) - limit;
        foreach (var current in polygon)
        {
            float distance = Vector2.Dot(current, normal) - limit;
            bool inside = keepAbove ? distance >= 0 : distance <= 0;
            bool previousInside = keepAbove ? previousDistance >= 0 : previousDistance <= 0;
            if (inside != previousInside)
                result.Add(Vector2.Lerp(previous, current, previousDistance / (previousDistance - distance)));
            if (inside) result.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return result.ToArray();
    }
}

internal readonly record struct PageCurlStrip(Vector2[] Polygon, Matrix3x2 Transform, bool IsBack, float Shade);
