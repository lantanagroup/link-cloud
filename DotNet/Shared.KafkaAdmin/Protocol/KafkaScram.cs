using System.Security.Cryptography;
using System.Text;

namespace LantanaGroup.Link.Shared.KafkaAdmin.Protocol;

/// <summary>
/// The client side of SCRAM (RFC 5802 / RFC 7677) for SCRAM-SHA-256 and SCRAM-SHA-512, the mechanisms Kafka brokers use.
/// The nonce is injectable so the RFC test vectors can be checked.
/// </summary>
public sealed class KafkaScram
{
    private readonly HashAlgorithmName _hash;
    private readonly string _user;
    private readonly string _password;
    private readonly string _nonce;
    private string _clientFirstBare = "";
    private byte[] _serverSignature = [];

    public KafkaScram(string mechanism, string user, string password, string? nonce = null)
    {
        _hash = mechanism.ToUpperInvariant() switch
        {
            "SCRAM-SHA-256" => HashAlgorithmName.SHA256,
            "SCRAM-SHA-512" => HashAlgorithmName.SHA512,
            _ => throw new ArgumentException("Unsupported SCRAM mechanism " + mechanism + ".", nameof(mechanism))
        };
        _user = user;
        _password = password;
        _nonce = nonce ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    }

    public byte[] ClientFirst()
    {
        _clientFirstBare = "n=" + _user.Replace("=", "=3D", StringComparison.Ordinal).Replace(",", "=2C", StringComparison.Ordinal) + ",r=" + _nonce;
        return Encoding.UTF8.GetBytes("n,," + _clientFirstBare);
    }

    public byte[] ClientFinal(byte[] serverFirstBytes)
    {
        var serverFirst = Encoding.UTF8.GetString(serverFirstBytes);
        var fields = Parse(serverFirst);
        if (fields.TryGetValue('e', out var serverError))
            throw new KafkaWireException("Kafka refused the sign-in: " + serverError);
        if (!fields.TryGetValue('r', out var nonce) || !nonce.StartsWith(_nonce, StringComparison.Ordinal) || nonce.Length <= _nonce.Length)
            throw new KafkaWireException("Kafka returned an invalid SCRAM nonce.");
        if (!fields.TryGetValue('s', out var saltText) || !fields.TryGetValue('i', out var iterationText)
            || !int.TryParse(iterationText, out var iterations) || iterations < 1)
            throw new KafkaWireException("Kafka returned an invalid SCRAM challenge.");

        var salt = Convert.FromBase64String(saltText);
        var length = _hash == HashAlgorithmName.SHA512 ? 64 : 32;
        var salted = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(_password), salt, iterations, _hash, length);
        var clientKey = Hmac(salted, "Client Key");
        var storedKey = _hash == HashAlgorithmName.SHA512 ? SHA512.HashData(clientKey) : SHA256.HashData(clientKey);
        var withoutProof = "c=biws,r=" + nonce;
        var authMessage = Encoding.UTF8.GetBytes(_clientFirstBare + "," + serverFirst + "," + withoutProof);
        var signature = Hmac(storedKey, authMessage);
        var proof = new byte[clientKey.Length];
        for (var index = 0; index < proof.Length; index++)
            proof[index] = (byte)(clientKey[index] ^ signature[index]);
        _serverSignature = Hmac(Hmac(salted, "Server Key"), authMessage);
        return Encoding.UTF8.GetBytes(withoutProof + ",p=" + Convert.ToBase64String(proof));
    }

    public void VerifyServerFinal(byte[] serverFinalBytes)
    {
        var fields = Parse(Encoding.UTF8.GetString(serverFinalBytes));
        if (fields.TryGetValue('e', out var serverError))
            throw new KafkaWireException("Kafka refused the sign-in: " + serverError);
        if (!fields.TryGetValue('v', out var verifier)
            || !CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(verifier), _serverSignature))
            throw new KafkaWireException("Kafka's SCRAM server signature did not verify.");
    }

    private byte[] Hmac(byte[] key, string text) => Hmac(key, Encoding.UTF8.GetBytes(text));

    private byte[] Hmac(byte[] key, byte[] data) =>
        _hash == HashAlgorithmName.SHA512 ? HMACSHA512.HashData(key, data) : HMACSHA256.HashData(key, data);

    private static Dictionary<char, string> Parse(string message)
    {
        var fields = new Dictionary<char, string>();
        foreach (var part in message.Split(','))
        {
            if (part.Length >= 2 && part[1] == '=')
                fields[part[0]] = part[2..];
        }

        return fields;
    }
}
