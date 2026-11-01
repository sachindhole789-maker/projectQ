using App.Application.Common.Options;
using App.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace App.Tests.Unit;

public sealed class PasswordHasherTests
{
    private static Pbkdf2PasswordHasher CreateHasher() =>
        new(Options.Create(new SecurityOptions { Pbkdf2Iterations = 100_000 }));

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hasher = CreateHasher();

        var hash = hasher.Hash("Str0ng!Password123");

        Assert.StartsWith("PBKDF2-SHA256$", hash);
        Assert.DoesNotContain("Str0ng!Password123", hash);
        Assert.True(hasher.Verify("Str0ng!Password123", hash));
    }

    [Fact]
    public void Verify_rejects_wrong_password_and_malformed_hash()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("Str0ng!Password123");

        Assert.False(hasher.Verify("wrong-password", hash));
        Assert.False(hasher.Verify("Str0ng!Password123", "not-a-hash"));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        var hasher = CreateHasher();

        Assert.NotEqual(hasher.Hash("same-password"), hasher.Hash("same-password"));
    }

    [Fact]
    public void NeedsRehash_detects_weaker_iteration_count()
    {
        var hasher = CreateHasher();
        var weakHash = "PBKDF2-SHA256$1000$AAAA$AAAA";

        Assert.True(hasher.NeedsRehash(weakHash));
        Assert.False(hasher.NeedsRehash(hasher.Hash("Str0ng!Password123")));
    }
}
