using System;

namespace ShadowViewer.Plugin.Local.Readers.Internal;

/// <summary>滚轮使用有限时长的平滑滚动；高精度增量直接跟随设备。</summary>
internal sealed class ReaderWheelScrollController
{
    private const double Duration = 0.24;
    private double distance;
    private double position;
    private double velocity;
    private double initialVelocity;
    private double elapsed;
    private float directDelta;
    private bool isPrecisionGesture;
    private double inputIdleTime = double.PositiveInfinity;

    public bool IsActive => distance != 0 || directDelta != 0;

    public void Reset()
    {
        distance = position = velocity = initialVelocity = elapsed = 0;
        directDelta = 0;
        isPrecisionGesture = false;
        inputIdleTime = double.PositiveInfinity;
    }

    public void AddDelta(float delta)
    {
        if (!float.IsFinite(delta) || delta == 0) return;

        // Windows 的标准滚轮刻度为 120；细粒度设备已提供连续位移和惯性。
        // 同一细粒度手势偶尔也会合并成 120 的倍数，不应在中途切回动画。
        bool precise = delta % 120 != 0 || (isPrecisionGesture && inputIdleTime < 0.1);
        if (precise)
        {
            float accumulated = directDelta;
            Reset();
            directDelta = accumulated + delta;
            isPrecisionGesture = true;
            inputIdleTime = 0;
            return;
        }

        isPrecisionGesture = false;
        inputIdleTime = 0;

        double remaining = distance - position;
        if (remaining * delta < 0)
        {
            remaining = 0;
            velocity = 0;
        }
        distance = remaining + delta;
        position = elapsed = 0;
        // 三次 Hermite 曲线延续当前速度并在目标处停下；限制切线以避免越过目标。
        initialVelocity = Math.CopySign(Math.Min(Math.Abs(velocity), 3 * Math.Abs(distance) / Duration), distance);
    }

    public float ConsumeDelta(float deltaTime)
    {
        if (float.IsFinite(deltaTime) && deltaTime > 0) inputIdleTime += deltaTime;
        float result = directDelta;
        directDelta = 0;
        if (distance == 0 || !float.IsFinite(deltaTime) || deltaTime <= 0) return result;

        elapsed = Math.Min(Duration, elapsed + deltaTime);
        double t = elapsed / Duration;
        double t2 = t * t;
        double t3 = t2 * t;
        double next = (t3 - 2 * t2 + t) * Duration * initialVelocity + (-2 * t3 + 3 * t2) * distance;
        velocity = (3 * t2 - 4 * t + 1) * initialVelocity + (-6 * t2 + 6 * t) * distance / Duration;
        result += (float)(next - position);
        position = next;
        if (elapsed >= Duration) Reset();
        return result;
    }
}
