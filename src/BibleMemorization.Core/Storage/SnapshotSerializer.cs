using System.Text;
using BibleMemorization.Core.Model;
using System.Text.Json;

namespace BibleMemorization.Core.Storage;

/// <summary>Raised when a save file cannot be read.</summary>
public sealed class SaveFileFormatException(string message) : Exception(message);

/// <summary>
/// Turns a library into bytes and back. Shared by every provider, so the .save file
/// and the localStorage entry hold the same format.
/// </summary>
public static class SnapshotSerializer
{
    public static string Serialize(LibrarySnapshot snapshot, bool pretty = true)
    {
        var context = pretty ? LibraryJsonContext.Pretty : LibraryJsonContext.Compact;
        return JsonSerializer.Serialize(snapshot, context.LibrarySnapshot);
    }

    public static byte[] SerializeToBytes(LibrarySnapshot snapshot) =>
        Encoding.UTF8.GetBytes(Serialize(snapshot));

    /// <summary>
    /// Reads a library back. Anything unreadable throws rather than returning a
    /// partial result — silently importing half a library would be worse than a
    /// clear error, since the user would not know what they had lost.
    /// </summary>
    public static LibrarySnapshot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LibrarySnapshot.Empty;
        }

        LibrarySnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize(json, LibraryJsonContext.Pretty.LibrarySnapshot);
        }
        catch (JsonException ex)
        {
            throw new SaveFileFormatException($"This does not look like a valid save file: {ex.Message}");
        }

        if (snapshot is null)
        {
            throw new SaveFileFormatException("The save file was empty.");
        }

        if (snapshot.SchemaVersion > LibrarySnapshot.CurrentSchemaVersion)
        {
            throw new SaveFileFormatException(
                $"This save file is version {snapshot.SchemaVersion}, but this app only understands "
                + $"version {LibrarySnapshot.CurrentSchemaVersion}. Update the app and try again.");
        }

        if (snapshot.SchemaVersion < 1)
        {
            throw new SaveFileFormatException(
                $"Unrecognised save file version {snapshot.SchemaVersion}.");
        }

        // Source-generated deserialization leaves absent properties null rather than
        // at their initializers, so a legal-looking {"schemaVersion":1} yields null
        // collections. Everything downstream assumes they are present, and the load
        // runs before the app renders — so an unguarded null is a blank page.
        return snapshot with
        {
            Passages = snapshot.Passages ?? [],
            Progress = snapshot.Progress ?? [],
            Settings = snapshot.Settings ?? AppSettings.Default,
        };
    }

    public static LibrarySnapshot DeserializeBytes(byte[] bytes) =>
        Deserialize(Encoding.UTF8.GetString(bytes));
}
