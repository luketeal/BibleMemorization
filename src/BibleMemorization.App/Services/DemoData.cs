using BibleMemorization.Core.Model;
using BibleMemorization.Core.Storage;

namespace BibleMemorization.App.Services;

/// <summary>
/// The library demo mode starts with, so the app has something to show immediately
/// rather than an empty shelf.
/// </summary>
public static class DemoData
{
    private static readonly DateTimeOffset Seeded = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public static LibrarySnapshot Snapshot() => new()
    {
        ExportedUtc = Seeded,
        Passages =
        [
            new Passage
            {
                Id = Guid.Parse("a0000000-0000-4000-8000-000000000001"),
                Title = "John 3:16",
                Reference = "John 3:16",
                Translation = "eng_kjv",
                TranslationName = "King James Version",
                Text = "For God so loved the world, that he gave his only begotten Son, "
                     + "that whosoever believeth in him should not perish, but have everlasting life.",
                Source = PassageSource.BibleApi,
                CreatedUtc = Seeded,
                ModifiedUtc = Seeded,
            },
            new Passage
            {
                Id = Guid.Parse("a0000000-0000-4000-8000-000000000002"),
                Title = "Psalm 23:1-3",
                Reference = "Psalm 23:1-3",
                Translation = "eng_kjv",
                TranslationName = "King James Version",
                Text = "The LORD is my shepherd; I shall not want. "
                     + "He maketh me to lie down in green pastures: he leadeth me beside the still waters. "
                     + "He restoreth my soul: he leadeth me in the paths of righteousness for his name's sake.",
                Source = PassageSource.BibleApi,
                CreatedUtc = Seeded,
                ModifiedUtc = Seeded.AddMinutes(-5),
            },
        ],
        Progress = [],
        Settings = AppSettings.Default,
    };
}
