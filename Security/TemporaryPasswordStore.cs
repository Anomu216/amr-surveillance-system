
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AMRSurveillance.Security;

public sealed class TemporaryPasswordStore
{
    private static readonly TimeSpan Lifetime =
        TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, PasswordEntry>
        _entries = new();

    public string Store(string userName, string password)
    {
        RemoveExpiredEntries();

        var reference = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        _entries[reference] = new PasswordEntry(
            userName,
            password,
            DateTimeOffset.UtcNow.Add(Lifetime));

        return reference;
    }

    public bool TryTake(
        string reference,
        out string userName,
        out string password)
    {
        userName = string.Empty;
        password = string.Empty;

        if (!_entries.TryRemove(reference, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        userName = entry.UserName;
        password = entry.Password;

        return true;
    }

    private void RemoveExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var item in _entries)
        {
            if (item.Value.ExpiresAt <= now)
            {
                _entries.TryRemove(item.Key, out _);
            }
        }
    }

    private sealed record PasswordEntry(
        string UserName,
        string Password,
        DateTimeOffset ExpiresAt);
}
