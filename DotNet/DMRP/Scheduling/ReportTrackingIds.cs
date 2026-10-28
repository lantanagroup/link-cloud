using System.Security.Cryptography;
using System.Text;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// A ReportTrackingId that is the same every time the same period is announced for the same
    /// facility. The Report service dead-letters a ReportScheduled whose id it has already stored,
    /// so a night that is re-fired - a misfire recovered, a pod restarted mid-loop, an operator
    /// triggering the job by hand - cannot create a second report for the same period.
    /// </summary>
    public static class ReportTrackingIds
    {
        // Fixed forever: changing it would make every id produced so far stop deduplicating.
        private static readonly Guid Namespace = new("6f1c2a4e-3b7d-4d3a-9c1e-2f0b5a8d7e61");

        public static Guid For(string facilityId, string frequency, DateTime periodStartUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
            ArgumentException.ThrowIfNullOrWhiteSpace(frequency);

            return CreateVersion5(Namespace, $"{facilityId}|{frequency}|{periodStartUtc:O}");
        }

        /// <summary>RFC 4122 section 4.3 name-based UUID with SHA-1.</summary>
        private static Guid CreateVersion5(Guid namespaceId, string name)
        {
            var namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            var nameBytes = Encoding.UTF8.GetBytes(name);
            var hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);

            var guid = new byte[16];
            Array.Copy(hash, guid, 16);

            guid[6] = (byte)((guid[6] & 0x0F) | 0x50); // version 5
            guid[8] = (byte)((guid[8] & 0x3F) | 0x80); // RFC 4122 variant

            SwapByteOrder(guid);
            return new Guid(guid);
        }

        // Guid.ToByteArray is little-endian in the first three groups; RFC 4122 hashes big-endian.
        private static void SwapByteOrder(byte[] guid)
        {
            Swap(guid, 0, 3);
            Swap(guid, 1, 2);
            Swap(guid, 4, 5);
            Swap(guid, 6, 7);
        }

        private static void Swap(byte[] bytes, int left, int right) =>
            (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
    }
}
