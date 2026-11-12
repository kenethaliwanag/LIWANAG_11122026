using CsvProcessing.Api.Infrastructure;

namespace CsvProcessing.Tests.Unit;

public class ApiKeyComparerTests
{
    [Fact]
    public void Matches_SameKey_ReturnsTrue()
    {
        Assert.True(ApiKeyComparer.Matches("s3cret-key", ["s3cret-key"]));
    }

    [Fact]
    public void Matches_DifferentKey_ReturnsFalse()
    {
        Assert.False(ApiKeyComparer.Matches("wrong-key", ["s3cret-key"]));
    }

    [Fact]
    public void Matches_KeyWithDifferentLength_ReturnsFalse()
    {
        Assert.False(ApiKeyComparer.Matches("short", ["a-much-longer-configured-key"]));
    }

    [Fact]
    public void Matches_EmptyPresentedKey_ReturnsFalse()
    {
        Assert.False(ApiKeyComparer.Matches("", ["s3cret-key"]));
    }

    [Fact]
    public void Matches_OneOfSeveralKeys_ReturnsTrue()
    {
        Assert.True(ApiKeyComparer.Matches("second", ["first", "second"]));
    }

    [Fact]
    public void Matches_NoConfiguredKeys_ReturnsFalse()
    {
        Assert.False(ApiKeyComparer.Matches("any", []));
    }
}
