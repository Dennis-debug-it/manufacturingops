using Ecosys.Api.Common;

namespace Ecosys.Tests;

/// <summary>
/// Locks in the shared CSV escaping used by every backend export (reports + import templates):
/// formula-injection neutralization and RFC-4180 quote doubling.
/// </summary>
public sealed class CsvHelperTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    public void Escape_NeutralizesFormulaPrefixes(string input, string expected)
    {
        Assert.Equal(expected, Csv.Escape(input));
    }

    [Fact]
    public void Escape_DoublesEmbeddedQuotes()
    {
        Assert.Equal("She said \"\"hi\"\"", Csv.Escape("She said \"hi\""));
    }

    [Fact]
    public void Escape_LeavesOrdinaryTextUntouched()
    {
        Assert.Equal("Acme Ltd", Csv.Escape("Acme Ltd"));
    }

    [Fact]
    public void Escape_HandlesNull()
    {
        Assert.Equal(string.Empty, Csv.Escape(null));
    }

    [Fact]
    public void Field_WrapsInQuotes_AndNeutralizes()
    {
        // Wrapped in quotes AND the formula prefix is neutralized with a leading apostrophe.
        Assert.Equal("\"'=danger\"", Csv.Field("=danger"));
    }

    [Fact]
    public void Row_QuotesAndEscapesEveryCell()
    {
        var row = Csv.Row(["Client, Inc", "=SUM(A1)", "plain"]);
        Assert.Equal("\"Client, Inc\",\"'=SUM(A1)\",\"plain\"", row);
    }
}
