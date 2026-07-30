namespace BibleMemorization.Core.Techniques;

/// <summary>
/// Resolves techniques by id. Registered in DI, so adding a technique is one class
/// plus one registration line.
/// </summary>
public sealed class TechniqueRegistry
{
    private readonly Dictionary<string, IMemoryTechnique> _byId;

    public TechniqueRegistry(IEnumerable<IMemoryTechnique> techniques)
    {
        All = techniques.ToArray();

        if (All.Count == 0)
        {
            throw new ArgumentException("At least one technique must be registered.", nameof(techniques));
        }

        _byId = All.ToDictionary(t => t.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<IMemoryTechnique> All { get; }

    public IMemoryTechnique Default => All[0];

    public IMemoryTechnique Get(string? id) =>
        id is not null && _byId.TryGetValue(id, out var technique) ? technique : Default;

    public bool Contains(string id) => _byId.ContainsKey(id);
}
