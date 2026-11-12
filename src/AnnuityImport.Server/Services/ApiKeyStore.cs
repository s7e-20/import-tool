using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NVGI.Projet.Importtool.Services;

public sealed class ApiKeyStore
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public string Issue()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _keys[key] = 0;
        return key;
    }

    public bool IsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key) && _keys.ContainsKey(key);

    public void Revoke(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _keys.TryRemove(key, out _);
    }
}
