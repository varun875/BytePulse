using System.Text;
using BytePulse.Core.Storage;
using Xunit;

namespace BytePulse.Core.Tests.Storage;

public sealed class NameStoreTests
{
    [Theory]
    [InlineData("readme.txt")]
    [InlineData("file with spaces.dat")]
    [InlineData("caf\u00e9.bin")]          // Latin-1 boundary
    [InlineData("\u00ff\u00fe\u00fd.bin")]  // Latin-1 boundary
    [InlineData("\ud83d\ude00emoji.txt")]   // surrogate pair, needs UTF-16
    [InlineData("\u4e2d\u6587.txt")]        // needs UTF-16
    [InlineData("")]
    public void RoundTripsNames(string name)
    {
        var store = new NameStore();

        int offset = store.Add(Encoding.Unicode.GetBytes(name));

        Assert.Equal(name, store.GetString(offset));
        Assert.Equal(name.Length, store.GetCharCount(offset));
    }

    [Fact]
    public void StoresNamesWithoutSharingStorage()
    {
        var store = new NameStore();

        int first = store.Add(Encoding.Unicode.GetBytes("alpha"));
        int second = store.Add(Encoding.Unicode.GetBytes("beta"));

        Assert.NotEqual(first, second);
        Assert.Equal("alpha", store.GetString(first));
        Assert.Equal("beta", store.GetString(second));
    }

    [Fact]
    public void UsesTheCompactEncodingForAsciiNames()
    {
        var asciiStore = new NameStore();
        int ascii = asciiStore.Add(Encoding.Unicode.GetBytes("abc"));
        int asciiNext = asciiStore.Add(Encoding.Unicode.GetBytes("x"));

        // "abc" is three Latin-1 bytes plus a three byte header.
        Assert.Equal(6, asciiNext - ascii);

        var wideStore = new NameStore();
        int wide = wideStore.Add(Encoding.Unicode.GetBytes("\u4e2d"));
        int wideNext = wideStore.Add(Encoding.Unicode.GetBytes("x"));

        // The same single character needs two bytes of UTF-16 payload.
        Assert.Equal(5, wideNext - wide);
    }

    [Fact]
    public void ComparesNamesIgnoringCase()
    {
        var store = new NameStore();

        int alpha = store.Add(Encoding.Unicode.GetBytes("Alpha"));
        int beta = store.Add(Encoding.Unicode.GetBytes("beta"));
        int betaUpper = store.Add(Encoding.Unicode.GetBytes("BETA"));

        Assert.True(store.CompareNames(alpha, beta) < 0);
        Assert.True(store.CompareNames(beta, alpha) > 0);

        // Names that differ only in case are still ordered deterministically, so the
        // comparison never reports them as equal.
        Assert.NotEqual(0, store.CompareNames(beta, betaUpper));
        Assert.Equal(
            -Math.Sign(store.CompareNames(beta, betaUpper)),
            Math.Sign(store.CompareNames(betaUpper, beta)));
    }

    [Fact]
    public void ComparesNamesOfDifferentLengths()
    {
        var store = new NameStore();

        int shortName = store.Add(Encoding.Unicode.GetBytes("ab"));
        int longName = store.Add(Encoding.Unicode.GetBytes("abc"));

        Assert.True(store.CompareNames(shortName, longName) < 0);
        Assert.True(store.CompareNames(longName, shortName) > 0);
    }

    [Fact]
    public void ReadsTheSameNamesOnceFrozen()
    {
        var store = new NameStore();
        int offset = store.Add(Encoding.Unicode.GetBytes("frozen.bin"));

        store.Freeze();

        Assert.Equal("frozen.bin", store.GetString(offset));
        Assert.Equal(10, store.GetCharCount(offset));
    }

    [Fact]
    public void RejectsNamesOnceFrozen()
    {
        var store = new NameStore();
        store.Freeze();

        Assert.Throws<ObjectDisposedException>(() => store.Add(Encoding.Unicode.GetBytes("late.bin")));
    }

    [Fact]
    public void RejectsAnOddNumberOfNameBytes()
    {
        var store = new NameStore();

        Assert.Throws<ArgumentException>(() => store.Add(new byte[] { 1, 2, 3 }));
    }
}
