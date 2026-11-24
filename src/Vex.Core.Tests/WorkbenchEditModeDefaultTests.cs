using Vex.Core.Models;
using Xunit;

namespace Vex.Core.Tests;

public sealed class WorkbenchEditModeDefaultTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("instant")]
    [InlineData("unknown")]
    public void MissingOrNonSourcePreferenceDefaultsToInstant(string? savedMode)
    {
        Assert.Equal(WorkbenchLayoutKeys.Instant, WorkbenchLayoutKeys.ResolveEditMode(savedMode));
    }

    [Fact]
    public void ExplicitSourcePreferenceIsPreserved()
    {
        Assert.Equal(WorkbenchLayoutKeys.Source, WorkbenchLayoutKeys.ResolveEditMode(WorkbenchLayoutKeys.Source));
    }
}
