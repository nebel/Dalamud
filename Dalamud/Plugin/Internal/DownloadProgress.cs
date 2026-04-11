using Serilog;

using System.Threading;

namespace Dalamud.Plugin.Internal;

public class DownloadProgress(string manifestName, bool useTesting) : IProgress<long>
{
    private readonly RateRingBuffer rate =
        new(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5));

    public long Current { get; private set; }

    public long Total { get; private set; }

    public double Percentage => this.Total == 0 ? 0 : ((double)this.Current / this.Total) * 100;

    public void SetTotal(long size)
    {
        this.Total = size;
    }

    public void Report(long value)
    {
        this.Current = value;

        this.rate.Report(value);

        Thread.Sleep(5); // TODO HACK just testing, remove this!!!

        Log.Information(
            "Downloaded {Bytes}/{Total} bytes ({Speed:F2} B/s)",
            this.Current,
            this.Total,
            this.rate.GetBytesPerSecond);
    }

    public string GetDescription()
    {
        var name = manifestName;
        if (useTesting)
            name += " (testing)";

        if (this.Total == 0)
        {
            if (this.Current == 0)
                return $"Downloading {name} ...";
            return $"Downloading {name} ... [{this.Current}] @ {this.rate.GetFormattedRate()}";
        }

        return $"Downloading {name} ... {this.Percentage:0.00}% [{this.Current}/{this.Total}] @ {this.rate.GetFormattedRate()}";
    }
}
