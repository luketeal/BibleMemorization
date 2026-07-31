using BibleMemorization.Core.Text;

namespace BibleMemorization.Core.ReadAlong;

/// <summary>
/// Turns a passage plus its hidden words into an alternating script of
/// "app speaks" and "user speaks" steps.
/// </summary>
public static class ReadAlongPlanner
{
    /// <summary>
    /// Builds the script. Consecutive visible words collapse into one spoken phrase
    /// and consecutive hidden words into one prompt, so the app reads in natural
    /// runs rather than stopping between every word.
    /// </summary>
    public static IReadOnlyList<ReadAlongStep> Plan(
        TokenizedPassage passage,
        IReadOnlyCollection<int> hiddenTokenIndices)
    {
        var steps = new List<ReadAlongStep>();

        var pendingSpoken = new List<Token>();
        var pendingHidden = new List<Token>();

        void FlushSpoken()
        {
            if (pendingSpoken.Count == 0)
            {
                return;
            }

            steps.Add(new SpeakStep
            {
                TokenIndices = pendingSpoken.Select(t => t.Index).ToArray(),
                // Rebuilt with its punctuation so the synthesizer phrases and pauses
                // the way the text reads.
                Text = string.Concat(pendingSpoken.Select(t => t.Original)).Trim(),
            });

            pendingSpoken.Clear();
        }

        void FlushHidden()
        {
            if (pendingHidden.Count == 0)
            {
                return;
            }

            steps.Add(new ListenStep
            {
                TokenIndices = pendingHidden.Select(t => t.Index).ToArray(),
                ExpectedWords = pendingHidden.Select(t => t.Word).ToArray(),
            });

            pendingHidden.Clear();
        }

        foreach (var token in passage)
        {
            var isHidden = token.IsWord && hiddenTokenIndices.Contains(token.Index);

            if (isHidden)
            {
                FlushSpoken();
                pendingHidden.Add(token);
                continue;
            }

            // Punctuation between two hidden words belongs with neither: attaching it
            // to the spoken run would make the app say a stray comma on its own.
            if (!token.IsWord && pendingHidden.Count > 0 && pendingSpoken.Count == 0)
            {
                continue;
            }

            FlushHidden();
            pendingSpoken.Add(token);
        }

        FlushHidden();
        FlushSpoken();

        return steps;
    }
}
