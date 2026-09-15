namespace HRMS.API.Services;

public static class LeaveBalanceCalculator
{
    public static int DaysInYear(DateTime from, DateTime to, int year)
    {
        var start = from.Date > new DateTime(year, 1, 1) ? from.Date : new DateTime(year, 1, 1);
        var end = to.Date < new DateTime(year, 12, 31) ? to.Date : new DateTime(year, 12, 31);
        return end < start ? 0 : (end - start).Days + 1;
    }
}
