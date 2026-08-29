using Alacrity.Launcher.Core;
using Xunit;

namespace Alacrity.Launcher.Core.Tests;

public sealed class ChangelogSearchSessionTests
{
    [Fact]
    public void RefiningAQueryStartsAtTheFirstLogicalMatch()
    {
        var session = new ChangelogSearchSession();
        const string text = "ore stone ore stone";

        ChangelogSearchMatch initial = session.UpdateQuery(text, "ore");
        Assert.Equal(0, initial.Start);

        ChangelogSearchMatch next = session.Move(text, previous: false);
        Assert.Equal(10, next.Start);

        ChangelogSearchMatch refined = session.UpdateQuery(text, "ore stone");
        Assert.Equal(0, refined.Start);
        Assert.Equal(1, refined.Current);
        Assert.Equal(2, refined.Total);
    }

    [Fact]
    public void EnterAndShiftEnterNavigationWrapsDeterministically()
    {
        var session = new ChangelogSearchSession();
        const string text = "one two one two one";

        Assert.Equal(0, session.UpdateQuery(text, "one").Start);
        Assert.Equal(8, session.Move(text, previous: false).Start);
        Assert.Equal(16, session.Move(text, previous: false).Start);
        Assert.Equal(0, session.Move(text, previous: false).Start);
        Assert.Equal(16, session.Move(text, previous: true).Start);
    }

    [Fact]
    public void MissingQueryReportsNotFound()
    {
        var session = new ChangelogSearchSession();

        Assert.False(session.UpdateQuery("Terraria", "Calamity").IsFound);
    }
}
