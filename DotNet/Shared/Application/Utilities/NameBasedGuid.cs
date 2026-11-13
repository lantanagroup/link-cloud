using System.Security.Cryptography;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Utilities;

/// <summary>
/// Creates name-based GUIDs: the same namespace and name always give the same GUID, on any machine.
/// </summary>
/// <remarks>
/// <para>
/// Use one for an id that must come out the same when work is repeated, such as a re-fired scheduled job. A
/// caller's namespace GUID and name format are part of its stored data: changing either changes every id it
/// produces.
/// </para>
/// <para>
/// The ids are predictable by design. Namespace GUIDs live in this public repository, so anyone who can guess
/// the name can compute the id. Never use one where an id must be hard to guess, such as an identifier in a
/// route that could be enumerated or anything that grants access; use <see cref="Guid.NewGuid"/> there.
/// </para>
/// </remarks>
public static class NameBasedGuid
{
    /// <summary>
    /// Creates a version 5 (SHA-1) name-based GUID, as defined in RFC 9562 section 5.5.
    /// </summary>
    /// <param name="namespaceId">
    /// The caller's own namespace GUID, fixed for good once ids have been stored.
    /// </param>
    /// <param name="name">
    /// The name within the namespace, encoded as UTF-8.
    /// </param>
    public static Guid CreateVersion5(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        var guid = new byte[16];
        Array.Copy(hash, guid, 16);

        // The version 5 nibble, then the RFC 9562 variant bits.
        guid[6] = (byte)((guid[6] & 0x0F) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80);

        SwapByteOrder(guid);
        return new Guid(guid);
    }

    // Guid.ToByteArray is little-endian in the first three groups; RFC 9562 hashes big-endian.
    private static void SwapByteOrder(byte[] guid)
    {
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }

    private static void Swap(byte[] bytes, int left, int right)
    {
        (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
    }
}
