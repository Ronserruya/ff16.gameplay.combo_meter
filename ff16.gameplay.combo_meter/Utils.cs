using System.Diagnostics;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

namespace ff16.gameplay.combo_meter;

public class LimitedList<T>
{
    public readonly int capacity;
    public readonly List<T> items;

    public LimitedList(int maxSize)
    {
        capacity = maxSize;
        items = new();
    }
    public bool Contains(T item)
    {
        return items.Contains(item);
    }
    public void Add(T item)
    {
        items.Remove(item);

        if (items.Count >= capacity)
            items.RemoveAt(0);
        items.Add(item);
    }
    public void Clear()
    {
        items.Clear();
    }

    public T? Last()
    {
        if (items.Count == 0)
            return default;
        return items.Last();
    }
}

public static class Extensions
{
    public static void AddScan(this IStartupScanner scans, string pattern, Action<nint> action)
    {
        var baseAddress = Process.GetCurrentProcess().MainModule!.BaseAddress;
        scans!.AddMainModuleScan(pattern, result =>
        {
            if (!result.Found)
                throw new Exception($"Scan unable to find pattern: {pattern}!");
            action(result.Offset + baseAddress);
        });
    }

    public static Action<TimeSpan> CreateDebouncedAction(this Action action)
    {

        CancellationTokenSource? cancellationTokenSource = null;

        return (TimeSpan delay) =>
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new();

            Task
                .Delay(delay, cancellationTokenSource.Token)
                .ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        action();
                    }
                }, TaskScheduler.Default);
        };
    }
}

public class CappedStopwatch : Stopwatch
{
    public TimeSpan MaxDuration;
    public CappedStopwatch(TimeSpan maxDuration)
    {
        MaxDuration = maxDuration;
    }

    public bool isElapsed()
    {
        return Elapsed >= MaxDuration;
    }

    public void Restart(TimeSpan maxDuration)
    {
        MaxDuration = maxDuration;
        Restart();
    }
}
