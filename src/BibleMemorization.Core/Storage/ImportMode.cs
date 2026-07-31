namespace BibleMemorization.Core.Storage;

/// <summary>What to do with the library already present when a save file is imported.</summary>
public enum ImportMode
{
    /// <summary>Keep existing passages and add the incoming ones.</summary>
    Merge,

    /// <summary>Discard everything and load only what the file holds.</summary>
    Replace,
}

/// <summary>What an import actually did, so the UI can report it honestly.</summary>
public sealed record ImportResult(int Added, int Updated, int Skipped, ImportMode Mode)
{
    public int Total => Added + Updated + Skipped;
}
