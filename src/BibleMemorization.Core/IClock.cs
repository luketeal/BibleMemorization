namespace BibleMemorization.Core;

/// <summary>
/// Time, behind a seam. Real time makes timestamps unstable, which turns
/// screenshots into false diffs and makes attempt history awkward to assert on.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>A clock that only moves when told to.</summary>
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    public static FixedClock AtDefault() => new(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

    public void Set(DateTimeOffset now) => UtcNow = now;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
