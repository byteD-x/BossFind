using FlaUI.UIA3;

namespace BossFind.Ui.Tests;

public sealed class UiPrerequisiteTests
{
    [Fact]
    public void FlaUi_uia3_assembly_is_available_for_interactive_smoke_tests()
    {
        Assert.NotNull(typeof(UIA3Automation).Assembly);
    }
}
