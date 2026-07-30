using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.Core.Tests.Storage;

public class SnapshotSerializerTests
{
    private static LibrarySnapshot Sample() => new()
    {
        ExportedUtc = new DateTimeOffset(2026, 3, 4, 10, 30, 0, TimeSpan.Zero),
        Passages =
        [
            new Passage
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "John 3:16",
                Reference = "John 3:16",
                Translation = "eng_kjv",
                TranslationName = "King James Version",
                Text = "For God so loved the world",
                Source = PassageSource.BibleApi,
                CreatedUtc = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
                ModifiedUtc = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
            },
        ],
        Progress =
        [
            new PracticeProgress
            {
                PassageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TechniqueId = "vanishing-text",
                HiddenTokenIndices = [1, 3, 5],
                Attempts =
                [
                    new Attempt
                    {
                        Utc = new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero),
                        TechniqueId = "vanishing-text",
                        InputMode = InputMode.Spoken,
                        Accuracy = 0.75,
                        DurationMs = 12_000,
                    },
                ],
            },
        ],
        Settings = AppSettings.Default with { DefaultTranslation = "eng_bsb", SpeechRate = 1.25 },
    };

    /// <summary>
    /// Compared as re-serialized JSON rather than with Assert.Equal on the records:
    /// record equality compares the collection properties by reference, so two
    /// snapshots holding identical data would still differ.
    /// </summary>
    [Fact]
    public void A_snapshot_survives_a_round_trip_intact()
    {
        var original = Sample();

        var json = SnapshotSerializer.Serialize(original);
        var restored = SnapshotSerializer.Deserialize(json);

        Assert.Equal(json, SnapshotSerializer.Serialize(restored));
    }

    [Fact]
    public void Round_tripping_preserves_hidden_words_and_attempts()
    {
        var restored = SnapshotSerializer.Deserialize(SnapshotSerializer.Serialize(Sample()));

        var progress = Assert.Single(restored.Progress);
        Assert.Equal([1, 3, 5], progress.HiddenTokenIndices);

        var attempt = Assert.Single(progress.Attempts);
        Assert.Equal(InputMode.Spoken, attempt.InputMode);
        Assert.Equal(0.75, attempt.Accuracy);
    }

    [Fact]
    public void Enums_serialize_as_readable_names()
    {
        // The .save file is something a user might open, so it should read sensibly.
        var json = SnapshotSerializer.Serialize(Sample());

        Assert.Contains("BibleApi", json);
        Assert.Contains("Spoken", json);
    }

    [Fact]
    public void The_compact_form_round_trips_too()
    {
        var original = Sample();

        var restored = SnapshotSerializer.Deserialize(SnapshotSerializer.Serialize(original, pretty: false));

        // Compact and pretty differ only in whitespace, so both must land on the
        // same data — that is what lets localStorage and the .save file interop.
        Assert.Equal(SnapshotSerializer.Serialize(original), SnapshotSerializer.Serialize(restored));
    }

    [Fact]
    public void Empty_input_yields_an_empty_library()
    {
        Assert.Equal(LibrarySnapshot.Empty, SnapshotSerializer.Deserialize(null));
        Assert.Equal(LibrarySnapshot.Empty, SnapshotSerializer.Deserialize("   "));
    }

    [Fact]
    public void Malformed_json_is_rejected_with_a_clear_error()
    {
        var ex = Assert.Throws<SaveFileFormatException>(() => SnapshotSerializer.Deserialize("{not json"));

        Assert.Contains("valid save file", ex.Message);
    }

    [Fact]
    public void A_file_from_a_newer_app_version_is_refused_rather_than_half_loaded()
    {
        var json = SnapshotSerializer.Serialize(Sample() with { SchemaVersion = 99 });

        var ex = Assert.Throws<SaveFileFormatException>(() => SnapshotSerializer.Deserialize(json));

        Assert.Contains("version 99", ex.Message);
    }

    [Fact]
    public void A_nonsensical_version_is_refused()
    {
        var json = SnapshotSerializer.Serialize(Sample() with { SchemaVersion = 0 });

        Assert.Throws<SaveFileFormatException>(() => SnapshotSerializer.Deserialize(json));
    }

    [Fact]
    public void Bytes_round_trip_as_utf8()
    {
        var original = Sample() with
        {
            Passages = [Sample().Passages[0] with { Text = "Café naïve — “quoted”" }],
        };

        var restored = SnapshotSerializer.DeserializeBytes(SnapshotSerializer.SerializeToBytes(original));

        Assert.Equal("Café naïve — “quoted”", restored.Passages[0].Text);
    }
}
