using System.Text.Json;
using FinanceProject.Models;

namespace FinanceProject.Tests;

internal static class ModelRegressionTests
{
    public static IEnumerable<RegressionCase> Cases()
    {
        yield return RegressionCase.Sync("Model: zero-contribution legacy pot remains unchanged", () =>
        {
            var pot = new SavingsSubPot { Name = "Legacy", Amount = 123.45m };
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 1, 15)), "Legacy initialization is a no-op");
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2026, 6, 1)), "Legacy catch-up is a no-op");
            Assert.Pot(pot, 123.45m, 0m, null);
        });
        yield return RegressionCase.Sync("Model: zero rate does not move an existing marker", () =>
        {
            var pot = NewPot(0m, new DateOnly(2023, 12, 1));
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 6, 1)), "Disabled pot is unchanged");
            Assert.Pot(pot, 100m, 0m, new DateOnly(2023, 12, 1));
        });
        yield return RegressionCase.Sync("Model: positive rate without marker initializes without allocation", () =>
        {
            var pot = NewPot(12.50m, null);
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 1, 31)), "Initialization changes tracking");
            Assert.Pot(pot, 100m, 12.50m, new DateOnly(2024, 1, 1));
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 1, 31)), "Initialization is idempotent");
        });
        yield return RegressionCase.Sync("Model: allocation occurs on next month's first day", () =>
        {
            var pot = NewPot(12.50m, new DateOnly(2024, 1, 1));
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 1, 31)), "No allocation before next month");
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 1)), "First day is eligible");
            Assert.Pot(pot, 112.50m, 12.50m, new DateOnly(2024, 2, 1));
        });
        yield return RegressionCase.Sync("Model: multiple missed months catch up with decimal precision", () =>
        {
            var pot = NewPot(0.01m, new DateOnly(2024, 2, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 7, 20)), "Five months are due");
            Assert.Pot(pot, 100.05m, 0.01m, new DateOnly(2024, 7, 1));
        });
        yield return RegressionCase.Sync("Model: December to January crosses the year once", () =>
        {
            var pot = NewPot(10m, new DateOnly(2023, 12, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 1, 1)), "January is due");
            Assert.Pot(pot, 110m, 10m, new DateOnly(2024, 1, 1));
        });
        yield return RegressionCase.Sync("Model: catch-up across multiple years counts months", () =>
        {
            var pot = NewPot(2.50m, new DateOnly(2022, 12, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 3, 1)), "Fifteen months are due");
            Assert.Pot(pot, 137.50m, 2.50m, new DateOnly(2024, 3, 1));
        });
        yield return RegressionCase.Sync("Model: leap February does not add extra contributions", () =>
        {
            var pot = NewPot(10m, new DateOnly(2024, 1, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 1)), "February first allocation");
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 29)), "Leap day is the same month");
            Assert.Pot(pot, 110m, 10m, new DateOnly(2024, 2, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 3, 1)), "March allocation");
            Assert.Pot(pot, 120m, 10m, new DateOnly(2024, 3, 1));
            var catchUp = NewPot(10m, new DateOnly(2024, 1, 1));
            Assert.True(catchUp.ApplyMonthlyContributions(new DateOnly(2024, 3, 1)), "Catch-up includes leap February");
            Assert.Pot(catchUp, 120m, 10m, new DateOnly(2024, 3, 1));
        });
        yield return RegressionCase.Sync("Model: repeated calls within a month are idempotent", () =>
        {
            var pot = NewPot(10m, new DateOnly(2024, 1, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 10)), "First call catches up");
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 10)), "Same day is a no-op");
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 2, 29)), "Later day is a no-op");
            Assert.Pot(pot, 110m, 10m, new DateOnly(2024, 2, 1));
        });
        yield return RegressionCase.Sync("Model: clock rollback neither allocates nor rewinds marker", () =>
        {
            var pot = NewPot(10m, new DateOnly(2024, 3, 1));
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2023, 12, 31)), "Past month is a no-op");
            Assert.False(pot.ApplyMonthlyContributions(new DateOnly(2024, 3, 1)), "Return to marker is a no-op");
            Assert.Pot(pot, 100m, 10m, new DateOnly(2024, 3, 1));
            Assert.True(pot.ApplyMonthlyContributions(new DateOnly(2024, 4, 1)), "Only next month is due");
            Assert.Pot(pot, 110m, 10m, new DateOnly(2024, 4, 1));
        });
        yield return RegressionCase.Sync("Model: legacy JSON defaults and modern JSON round-trip", () =>
        {
            var legacy = JsonSerializer.Deserialize<SavingsSubPot>("{\"Name\":\"Legacy\",\"Amount\":0}")!;
            Assert.Equal("Legacy", legacy.Name, "Legacy name");
            Assert.Pot(legacy, 0m, 0m, null);
            Assert.False(legacy.ApplyMonthlyContributions(new DateOnly(2024, 2, 1)), "Legacy JSON stays inactive");
            var positive = JsonSerializer.Deserialize<SavingsSubPot>("{\"Name\":\"Positive\",\"Amount\":12.34,\"MonthlyContribution\":5.67}")!;
            Assert.True(positive.ApplyMonthlyContributions(new DateOnly(2024, 2, 29)), "Missing marker initializes");
            Assert.Pot(positive, 12.34m, 5.67m, new DateOnly(2024, 2, 1));
            var restored = JsonSerializer.Deserialize<SavingsSubPot>(JsonSerializer.Serialize(positive))!;
            Assert.Equal(positive.Name, restored.Name, "Round-trip name");
            Assert.Pot(restored, 12.34m, 5.67m, new DateOnly(2024, 2, 1));
            Assert.False(restored.ApplyMonthlyContributions(new DateOnly(2024, 2, 29)), "Restored marker prevents duplicate");
        });
        yield return RegressionCase.Sync("Model: DataAnnotations accept zero and positive balances/rates", () =>
        {
            foreach (var amount in new[] { 0m, 0.01m, 123.45m })
            foreach (var rate in new[] { 0m, 0.01m, 25m })
            {
                var pot = new SavingsSubPot { Name = "Valid", Amount = amount, MonthlyContribution = rate };
                Assert.Equal(0, Assert.Validate(pot).Count, $"Validation for amount {amount}, rate {rate}");
            }
        });
        yield return RegressionCase.Sync("Model: DataAnnotations reject each negative monetary field", () =>
        {
            foreach (var member in new[] { nameof(SavingsSubPot.Amount), nameof(SavingsSubPot.MonthlyContribution) })
            {
                var pot = new SavingsSubPot
                {
                    Name = "Invalid",
                    Amount = member == nameof(SavingsSubPot.Amount) ? -0.01m : 0m,
                    MonthlyContribution = member == nameof(SavingsSubPot.MonthlyContribution) ? -0.01m : 0m
                };
                var errors = Assert.Validate(pot);
                Assert.Equal(1, errors.Count, $"Only {member} is invalid");
                Assert.True(errors[0].MemberNames.Contains(member), $"Validation identifies {member}");
            }
        });
    }

    private static SavingsSubPot NewPot(decimal rate, DateOnly? month)
        => new() { Name = "Pot", Amount = 100m, MonthlyContribution = rate, LastContributionMonth = month };
}
