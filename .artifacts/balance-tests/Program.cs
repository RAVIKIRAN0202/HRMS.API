using HRMS.API.Services;
var cases = new[] {
    ("2026-01-01", "2026-01-01", 2026, 1),
    ("2026-12-30", "2027-01-03", 2026, 2),
    ("2026-12-30", "2027-01-03", 2027, 3),
    ("2024-02-28", "2024-03-01", 2024, 3),
    ("2026-01-01", "2026-01-03", 2027, 0)
};
foreach (var (from, to, year, expected) in cases)
    if (LeaveBalanceCalculator.DaysInYear(DateTime.Parse(from), DateTime.Parse(to), year) != expected) throw new Exception("Incorrect day count.");
Console.WriteLine("5 leave day-count checks passed.");
