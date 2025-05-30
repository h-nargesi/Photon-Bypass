using PhotonBypass.Tools;

namespace PhotonBypass.Test.BasicFunctions.Shared;

public class PersianHandlerTest
{
    [Fact]
    public void ToPersianString_ShouldReturnTwoDigitStringString()
    {
        var result = DateTime.Parse("2025-05-28").ToPersianString();

        Assert.Equal("1404/03/07", result);
    }

    [Fact]
    public void ToPersianString_ShouldReturnTwoDigitStringString_WithFormat()
    {
        var result = DateTime.Parse("2025-05-28").ToPersianDayOfMonth();

        Assert.Equal("07", result);
    }

    [Fact]
    public void MonthToDays_ShouldAddMouthAndReturnDays()
    {
        var result = DateTime.Parse("2025-05-28").AddMonthToDays(2);

        Assert.Equal(62, result);
    }
}
