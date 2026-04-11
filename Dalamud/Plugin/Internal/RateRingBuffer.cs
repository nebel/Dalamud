using System.Diagnostics;

namespace Dalamud.Plugin.Internal;

public sealed class RateRingBuffer
{
    private readonly long[] buckets;
    private readonly long resolutionTicks;
    private readonly int bucketCount;

    private long startTimestamp;
    private bool started;

    private int currentIndex;
    private long runningSum;

    private long lastValue;
    private long lastTick;

    public RateRingBuffer(TimeSpan resolution, TimeSpan windowLength)
    {
        if (resolution <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(resolution));

        if (windowLength <= resolution)
            throw new ArgumentOutOfRangeException(nameof(windowLength));

        this.resolutionTicks = resolution.Ticks;
        this.bucketCount = (int)Math.Ceiling(
            windowLength.Ticks / (double)this.resolutionTicks);

        this.buckets = new long[this.bucketCount];
        this.startTimestamp = Stopwatch.GetTimestamp();
    }

    public void Report(long absoluteValue)
    {
        if (absoluteValue <= 0)
            return;

        if (!this.started)
        {
            this.startTimestamp = Stopwatch.GetTimestamp();
            this.lastValue = absoluteValue;
            this.lastTick = 0;
            this.started = true;
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        var elapsedTicks = (timestamp - this.startTimestamp)
                           * TimeSpan.TicksPerSecond
                           / Stopwatch.Frequency;

        var tickIndex = elapsedTicks / this.resolutionTicks;
        var deltaTicks = tickIndex - this.lastTick;
        this.lastTick = tickIndex;

        var deltaBytes = absoluteValue - this.lastValue;
        this.lastValue = absoluteValue;

        if (deltaTicks > 0)
        {
            var steps = Math.Min(deltaTicks, this.bucketCount);

            for (var i = 0; i < steps; i++)
            {
                this.currentIndex = (this.currentIndex + 1) % this.bucketCount;
                this.runningSum -= this.buckets[this.currentIndex];
                this.buckets[this.currentIndex] = 0;
            }
        }

        this.buckets[this.currentIndex] += deltaBytes;
        this.runningSum += deltaBytes;
    }

    public double GetBytesPerSecond()
    {
        if (!this.started)
            return 0;

        var windowSeconds = (this.bucketCount * this.resolutionTicks) / (double)TimeSpan.TicksPerSecond;

        return this.runningSum / windowSeconds;
    }

    public string GetFormattedRate()
    {
        var bytesPerSecond = this.GetBytesPerSecond();

        if (bytesPerSecond <= 0)
            return "0.00 B/s";

        const double KiB = 1024d;
        const double MiB = 1024d * 1024d;

        double value;
        string unit;

        if (bytesPerSecond >= MiB)
        {
            value = bytesPerSecond / MiB;
            unit = "MiB/s";
        }
        else if (bytesPerSecond >= KiB)
        {
            value = bytesPerSecond / KiB;
            unit = "KiB/s";
        }
        else
        {
            value = bytesPerSecond;
            unit = "B/s";
        }

        return $"{value:0.00} {unit}";
    }
}
