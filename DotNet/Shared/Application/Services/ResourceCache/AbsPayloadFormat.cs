using System.Text;

namespace LantanaGroup.Link.Shared.Application.Services.ResourceCache
{
    /// <summary>
    /// The line format of the resource cache's append blobs, and how it is cut into append blocks.
    /// </summary>
    /// <remarks>
    /// The payload blob holds a line pair per resource (reference, then JSON) and the ids blob a line per
    /// reference. Each block is appended atomically, so cutting only at record boundaries means a failed
    /// or retried append can duplicate whole records but can never leave a torn line for the next append
    /// to land on. See docs-dev/resource-cache.md.
    /// </remarks>
    public static class AbsPayloadFormat
    {
        /// <summary>
        /// The size records are packed up to before a new block is started.
        /// </summary>
        public const int TargetBlockBytes = 4 * 1024 * 1024;

        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// The payload record for one resource: its reference line, then its JSON line.
        /// </summary>
        public static string PayloadRecord(string reference, string json) =>
            reference + "\n" + json + "\n";

        /// <summary>
        /// The ids record for one resource: its reference line.
        /// </summary>
        public static string IdRecord(string reference) =>
            reference + "\n";

        /// <summary>
        /// Packs whole records into blocks of at most <paramref name="targetBlockBytes"/>, never splitting one.
        /// </summary>
        /// <remarks>
        /// A record larger than the target gets a block of its own, because splitting it is exactly what
        /// this exists to prevent.
        /// </remarks>
        /// <param name="records">Each record with the reference it belongs to, used only to name a failure.</param>
        /// <param name="targetBlockBytes">The size to pack up to.</param>
        /// <param name="maxBlockBytes">The largest block the store accepts.</param>
        /// <returns>The UTF-8 blocks, in record order.</returns>
        /// <exception cref="InvalidOperationException">
        /// A single record is larger than <paramref name="maxBlockBytes"/>, so it cannot be appended atomically.
        /// </exception>
        public static List<byte[]> BuildBlocks(IEnumerable<(string Reference, string Record)> records,
                                               int targetBlockBytes,
                                               long maxBlockBytes)
        {
            ArgumentNullException.ThrowIfNull(records);

            var target = Math.Min(targetBlockBytes, maxBlockBytes);
            var blocks = new List<byte[]>();
            var current = new MemoryStream();

            foreach (var (reference, record) in records)
            {
                var bytes = Utf8.GetBytes(record);

                if (bytes.Length > maxBlockBytes)
                {
                    throw new InvalidOperationException(
                        $"Resource cache record for '{reference}' is {bytes.Length} bytes, larger than the "
                        + $"{maxBlockBytes}-byte append block limit, so it cannot be written atomically.");
                }

                if (current.Length > 0 && current.Length + bytes.Length > target)
                {
                    blocks.Add(current.ToArray());
                    current = new MemoryStream();
                }

                current.Write(bytes);
            }

            if (current.Length > 0)
            {
                blocks.Add(current.ToArray());
            }

            return blocks;
        }

        /// <summary>
        /// Reads the payload's line pairs, handing each distinct reference's JSON to <paramref name="onPair"/>.
        /// </summary>
        /// <remarks>
        /// The first copy of a reference wins. A retried or concurrent append can repeat a reference, and
        /// collapsing it here makes that wasted bytes rather than a duplicate resource.
        /// </remarks>
        /// <param name="reader">The payload blob's content.</param>
        /// <param name="onPair">Called with the reference and its JSON, once per distinct reference.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>How many repeated references were skipped.</returns>
        public static async Task<int> ReadPairsAsync(TextReader reader,
                                                     Action<string, string> onPair,
                                                     CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reader);
            ArgumentNullException.ThrowIfNull(onPair);

            var seenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateCount = 0;

            while (true)
            {
                var reference = await reader.ReadLineAsync(cancellationToken);
                if (reference == null)
                {
                    break;
                }

                var json = await reader.ReadLineAsync(cancellationToken);
                if (json == null)
                {
                    break;
                }

                if (!seenReferences.Add(reference))
                {
                    duplicateCount++;
                    continue;
                }

                onPair(reference, json);
            }

            return duplicateCount;
        }
    }
}
