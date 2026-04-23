using OpenDeepWiki.Infrastructure;
using Xunit;

namespace OpenDeepWiki.Tests.Infrastructure;

public class RepositoryRouteDecoderUnitTests
{
    [Fact]
    public void DecodeRouteSegment_EncodedValue_ReturnsDecodedValue()
    {
        var result = RepositoryRouteDecoder.DecodeRouteSegment("hello%20world");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void DecodeRouteSegment_ValueWithoutPercent_ReturnsOriginalValue()
    {
        const string value = "hello world";

        var result = RepositoryRouteDecoder.DecodeRouteSegment(value);

        Assert.Equal(value, result);
    }

    [Fact]
    public void DecodeRouteSegment_InvalidEncoding_ReturnsOriginalValue()
    {
        const string value = "hello%2";

        var result = RepositoryRouteDecoder.DecodeRouteSegment(value);

        Assert.Equal(value, result);
    }
}
