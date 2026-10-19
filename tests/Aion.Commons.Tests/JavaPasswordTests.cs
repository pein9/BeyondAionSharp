using Aion.Commons.Network;

namespace Aion.Commons.Tests;

public sealed class JavaPasswordTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("secret", "secret", true)]
    [InlineData("secret", "Secret", false)]
    [InlineData("secret", "secretx", false)]
    [InlineData("é😀", "é😀", true)]
    [InlineData("é", "e\u0301", false)]
    public void CompareUtf8BytesWithoutCaseFoldingOrNormalization(string expected, string received, bool equal) =>
        Assert.Equal(equal, JavaPassword.EqualsUtf8(expected, received));

    [Fact]
    public void MalformedUtf16UsesJavaQuestionMarkReplacement()
    {
        Assert.True(JavaPassword.EqualsUtf8("?", new string((char)0xD800, 1)));
        Assert.True(JavaPassword.EqualsUtf8("?", new string((char)0xDC00, 1)));
        Assert.True(JavaPassword.EqualsUtf8("a?b", "a" + (char)0xD800 + "b"));
        Assert.False(JavaPassword.EqualsUtf8("\uFFFD", new string((char)0xD800, 1)));
    }
}
