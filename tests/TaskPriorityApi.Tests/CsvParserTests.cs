using Xunit;
using TaskPriorityApi.Utils;

namespace TaskPriorityApi.Tests;

public class CsvParserTests
{
    [Fact]
    public void ParsesHeaderAndRows()
    {
        var csv = "title,deadline_days,assignee,tags\nFix login,1,alice,urgent;bug\nUpdate docs,30,bob,docs";

        var rows = CsvParser.Parse(csv);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "title", "deadline_days", "assignee", "tags" }, rows[0]);
        Assert.Equal("Fix login", rows[1][0]);
        Assert.Equal("1", rows[1][1]);
    }

    [Fact]
    public void HandlesQuotedFieldsWithCommas()
    {
        var csv = "title,deadline_days,assignee,tags\n\"Fix, verify and deploy\",2,,\"urgent\"";

        var rows = CsvParser.Parse(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Fix, verify and deploy", rows[1][0]);
        Assert.Equal("urgent", rows[1][3]);
    }

    [Fact]
    public void SkipsEmptyLines()
    {
        var csv = "title,deadline_days,assignee,tags\n\nFix login,1,a,b\n";

        var rows = CsvParser.Parse(csv);

        Assert.Equal(2, rows.Count);
    }
}
