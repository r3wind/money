using System.Text.Json;
using FinanceProject.Models;
using FinanceProject.Services;

namespace FinanceProject.Tests;

internal static class StateRegressionTests
{
    public static IEnumerable<RegressionCase> Cases()
    {
        yield return new("State: add saves current tracking and starts next month", async () =>
        {
            var fixture = new StateFixture(2024, 1, 31);
            await fixture.State.AddSavingsSubPotAsync(Pot("Holiday", 0m, 12.50m, new DateOnly(2020, 1, 1)));
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 0m, 12.50m, new DateOnly(2024, 1, 1));
            Assert.Pot(fixture.SavedPots.Single(), 0m, 12.50m, new DateOnly(2024, 1, 1));
            fixture.AssertOnlySubPotsSaved(1);
            var sameMonth = fixture.FreshState();
            await sameMonth.LoadAsync();
            Assert.Pot(sameMonth.SavingsSubPots.Single(), 0m, 12.50m, new DateOnly(2024, 1, 1));
            fixture.AssertOnlySubPotsSaved(1);
            fixture.Clock.Set(2024, 2, 1);
            var nextMonth = fixture.FreshState();
            await nextMonth.LoadAsync();
            Assert.Pot(nextMonth.SavingsSubPots.Single(), 12.50m, 12.50m, new DateOnly(2024, 2, 1));
            Assert.Pot(fixture.SavedPots.Single(), 12.50m, 12.50m, new DateOnly(2024, 2, 1));
            fixture.AssertOnlySubPotsSaved(2);
        });
        yield return new("State: adding a zero-rate pot clears supplied tracking", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Disabled", 0m, 0m, new DateOnly(2023, 12, 1)));
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 0m, 0m, null);
            Assert.Pot(fixture.SavedPots.Single(), 0m, 0m, null);
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: legacy JSON migrates identity once without monetary changes", async () =>
        {
            var fixture = new StateFixture();
            const string legacy = "[{\"Name\":\"Legacy\",\"Amount\":0}]";
            fixture.Storage.Storage[StateFixture.SubPotsKey] = legacy;
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 0m, 0m, null);
            var migrated = fixture.State.SavingsSubPots.Single();
            Assert.Equal("Legacy", migrated.Name, "Legacy name unchanged");
            Assert.True(migrated.Id != Guid.Empty, "Legacy ID initialized");
            Assert.Equal(migrated.Id, fixture.SavedPots.Single().Id, "Migrated ID persisted");
            Assert.Pot(fixture.SavedPots.Single(), 0m, 0m, null);
            var payload = fixture.Storage.Storage[StateFixture.SubPotsKey];
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(migrated.Id, fresh.SavingsSubPots.Single().Id, "Fresh reload preserves identity");
            Assert.Equal("Legacy", fresh.SavingsSubPots.Single().Name, "Reload preserves name");
            Assert.Pot(fresh.SavingsSubPots.Single(), 0m, 0m, null);
            Assert.Equal(payload, fixture.Storage.Storage[StateFixture.SubPotsKey], "Reload does not rewrite migrated payload");
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: load initializes missing positive marker and persists once", async () =>
        {
            var fixture = new StateFixture(2024, 2, 29);
            fixture.Storage.Storage[StateFixture.SubPotsKey] = "[{\"Name\":\"Migrated\",\"Amount\":100,\"MonthlyContribution\":10}]";
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 100m, 10m, new DateOnly(2024, 2, 1));
            Assert.Pot(fixture.SavedPots.Single(), 100m, 10m, new DateOnly(2024, 2, 1));
            await fixture.FreshState().LoadAsync();
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: load catch-up persists and fresh reloads never duplicate", async () =>
        {
            var fixture = new StateFixture(2024, 3, 1);
            fixture.Seed(Pot("Catch-up", 100m, 10m, new DateOnly(2023, 12, 1)));
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 130m, 10m, new DateOnly(2024, 3, 1));
            Assert.Pot(fixture.SavedPots.Single(), 130m, 10m, new DateOnly(2024, 3, 1));
            fixture.AssertOnlySubPotsSaved(1);
            var payload = fixture.Storage.Storage[StateFixture.SubPotsKey];
            await fixture.State.LoadAsync();
            for (var i = 0; i < 3; i++)
            {
                var fresh = fixture.FreshState();
                await fresh.LoadAsync();
                Assert.Pot(fresh.SavingsSubPots.Single(), 130m, 10m, new DateOnly(2024, 3, 1));
            }
            Assert.Equal(payload, fixture.Storage.Storage[StateFixture.SubPotsKey], "Repeated loads keep saved payload");
            Assert.Equal(5, fixture.Storage.LoadedKeys.Count(key => key == StateFixture.SubPotsKey), "Each load reaches fake storage");
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: clock rollback does not save or reallocate", async () =>
        {
            var fixture = new StateFixture(2024, 2, 29);
            fixture.Seed(Pot("Future", 100m, 10m, new DateOnly(2024, 3, 1)));
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 100m, 10m, new DateOnly(2024, 3, 1));
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.LoadAsync();
            fixture.AssertOnlySubPotsSaved(0);
            fixture.Clock.Set(2024, 4, 1);
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.SavedPots.Single(), 110m, 10m, new DateOnly(2024, 4, 1));
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: multiple pots allocate independently in a single save", async () =>
        {
            var fixture = new StateFixture(2024, 3, 1);
            fixture.Seed(
                Pot("Overdue", 100m, 10m, new DateOnly(2023, 12, 1)),
                Pot("One month", 20m, 2.50m, new DateOnly(2024, 2, 1)),
                Pot("Untracked", 0m, 5m, null),
                Pot("Disabled", 75m, 0m, null),
                Pot("Current", 40m, 4m, new DateOnly(2024, 3, 1)),
                Pot("Future", 60m, 6m, new DateOnly(2024, 4, 1)));
            await fixture.State.LoadAsync();
            void Check(List<SavingsSubPot> pots)
            {
                Assert.Equal(6, pots.Count, "All pots retained");
                Assert.Pot(pots.Single(p => p.Name == "Overdue"), 130m, 10m, new DateOnly(2024, 3, 1));
                Assert.Pot(pots.Single(p => p.Name == "One month"), 22.50m, 2.50m, new DateOnly(2024, 3, 1));
                Assert.Pot(pots.Single(p => p.Name == "Untracked"), 0m, 5m, new DateOnly(2024, 3, 1));
                Assert.Pot(pots.Single(p => p.Name == "Disabled"), 75m, 0m, null);
                Assert.Pot(pots.Single(p => p.Name == "Current"), 40m, 4m, new DateOnly(2024, 3, 1));
                Assert.Pot(pots.Single(p => p.Name == "Future"), 60m, 6m, new DateOnly(2024, 4, 1));
            }
            Check(fixture.State.SavingsSubPots);
            Check(fixture.SavedPots);
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: contributions leave bank, savings accounts and other collections untouched", async () =>
        {
            var fixture = new StateFixture(2024, 1, 15);
            fixture.Storage.Seed("finance_bankbalance", 1234.56m);
            fixture.Storage.Seed("finance_savingspots", new[] { new SavingsPot { Name = "Account", Amount = 500m, Aer = 4.25m } });
            fixture.Storage.Seed("finance_creditcards", new[] { new CreditCard { Name = "Card", Limit = 1000m, AvailableCredit = 800m } });
            fixture.Storage.Seed("finance_directdebits", new[] { new DirectDebit { Name = "Rent", Amount = 300m, DayOfMonth = 7 } });
            fixture.Storage.Seed("finance_budgetcategories", new[] { new BudgetCategory { Name = "Food", MonthlyAmount = 150m } });
            fixture.Storage.Seed("finance_upcomingcosts", new[] { new UpcomingCost { Name = "Repair", Amount = 45m, Date = new DateTime(2024, 6, 1) } });
            fixture.Storage.Seed("finance_oneoffpayments", new[] { new OneOffPayment { Name = "Purchase", Amount = 20m, Date = new DateTime(2024, 1, 3) } });
            fixture.Storage.Seed("finance_incomes", new[] { new Income { Name = "Salary", Amount = 2000m, PaidThisMonth = true } });
            fixture.Seed(Pot("Allocation", 100m, 10m, new DateOnly(2024, 1, 1)));
            await fixture.State.LoadAsync();
            var before = OtherData(fixture.State);
            var storedBefore = fixture.Storage.Storage.Where(pair => pair.Key != StateFixture.SubPotsKey).ToDictionary();
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.LoadAsync();
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 120m, 10m, new DateOnly(2024, 3, 1));
            Assert.Equal(1234.56m, fixture.State.BankBalance, "Bank balance");
            Assert.Equal(500m, fixture.State.SavingsPots.Single().Amount, "Savings account balance");
            Assert.Equal(before, OtherData(fixture.State), "All unrelated in-memory data unchanged");
            foreach (var (key, json) in storedBefore)
                Assert.Equal(json, fixture.Storage.Storage[key], $"Stored {key} unchanged");
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("State: rate edit settles overdue months at previous rate and preserves tracking", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Rate", 100m, 10m, null));
            fixture.Clock.Set(2024, 4, 15);
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Rate", 100m, 25m, new DateOnly(2000, 1, 1), Guid.Empty));
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 130m, 25m, new DateOnly(2024, 4, 1));
            Assert.Pot(fixture.SavedPots.Single(), 130m, 25m, new DateOnly(2024, 4, 1));
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Rate", 130m, 30m, null, Guid.Empty));
            Assert.Pot(fixture.SavedPots.Single(), 130m, 30m, new DateOnly(2024, 4, 1));
            fixture.Clock.Set(2024, 5, 1);
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Pot(fresh.SavingsSubPots.Single(), 160m, 30m, new DateOnly(2024, 5, 1));
            fixture.AssertOnlySubPotsSaved(4);
        });
        yield return new("State: rename uses originalName and preserves tracking without duplicates", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Original", 100m, 10m, null));
            await fixture.State.AddSavingsSubPotAsync(Pot("Other", 20m, 0m, null));
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Renamed", 100m, 10m, null, Guid.Empty), originalName: "Original");
            Assert.Equal(2, fixture.State.SavingsSubPots.Count, "Rename replaces instead of adding");
            Assert.False(fixture.State.SavingsSubPots.Any(p => p.Name == "Original"), "Old name removed");
            Assert.Pot(fixture.SavedPots.Single(p => p.Name == "Renamed"), 120m, 10m, new DateOnly(2024, 3, 1));
            Assert.Pot(fixture.SavedPots.Single(p => p.Name == "Other"), 20m, 0m, null);
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(2, fresh.SavingsSubPots.Count, "Saved rename survives reload");
            fixture.AssertOnlySubPotsSaved(3);
        });
        yield return new("State: disable settles due contributions and clears marker", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Disable", 100m, 10m, null));
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Disable", 100m, 0m, new DateOnly(2000, 1, 1), Guid.Empty));
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 120m, 0m, null);
            Assert.Pot(fixture.SavedPots.Single(), 120m, 0m, null);
            fixture.Clock.Set(2024, 6, 1);
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Pot(fresh.SavingsSubPots.Single(), 120m, 0m, null);
            fixture.AssertOnlySubPotsSaved(2);
        });
        yield return new("State: re-enable starts next month without backfilling disabled interval", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Resume", 100m, 10m, null));
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Resume", 100m, 0m, null, Guid.Empty));
            fixture.Clock.Set(2024, 6, 20);
            var resumed = fixture.FreshState();
            await resumed.LoadAsync();
            await resumed.UpdateSavingsSubPotAsync(Pot("Resume", 120m, 15m, new DateOnly(2024, 3, 1), Guid.Empty));
            Assert.Pot(resumed.SavingsSubPots.Single(), 120m, 15m, new DateOnly(2024, 6, 1));
            Assert.Pot(fixture.SavedPots.Single(), 120m, 15m, new DateOnly(2024, 6, 1));
            await fixture.FreshState().LoadAsync();
            fixture.AssertOnlySubPotsSaved(3);
            fixture.Clock.Set(2024, 7, 1);
            var nextMonth = fixture.FreshState();
            await nextMonth.LoadAsync();
            Assert.Pot(nextMonth.SavingsSubPots.Single(), 135m, 15m, new DateOnly(2024, 7, 1));
            Assert.Pot(fixture.SavedPots.Single(), 135m, 15m, new DateOnly(2024, 7, 1));
            fixture.AssertOnlySubPotsSaved(4);
        });
        yield return new("State: amount edit preserves tracker and adds only overdue contributions", async () =>
        {
            var fixture = new StateFixture();
            await fixture.State.AddSavingsSubPotAsync(Pot("Amount", 100m, 10m, null));
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Amount", 0m, 10m, new DateOnly(2000, 1, 1), Guid.Empty));
            Assert.Pot(fixture.SavedPots.Single(), 0m, 10m, new DateOnly(2024, 1, 1));
            fixture.Clock.Set(2024, 3, 1);
            await fixture.State.UpdateSavingsSubPotAsync(Pot("Amount", 50m, 10m, null, Guid.Empty));
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 70m, 10m, new DateOnly(2024, 3, 1));
            Assert.Pot(fixture.SavedPots.Single(), 70m, 10m, new DateOnly(2024, 3, 1));
            await fixture.FreshState().LoadAsync();
            fixture.AssertOnlySubPotsSaved(3);
        });
        yield return new("State: same-instance amount edit does not double-apply catch-up", async () =>
        {
            var fixture = new StateFixture();
            var pot = Pot("Same instance", 100m, 10m, null);
            await fixture.State.AddSavingsSubPotAsync(pot);
            fixture.Clock.Set(2024, 3, 1);
            pot.Amount = 50m;
            await fixture.State.UpdateSavingsSubPotAsync(pot);
            Assert.Pot(pot, 70m, 10m, new DateOnly(2024, 3, 1));
            Assert.Pot(fixture.SavedPots.Single(), 70m, 10m, new DateOnly(2024, 3, 1));
            await fixture.State.UpdateSavingsSubPotAsync(pot);
            Assert.Pot(fixture.SavedPots.Single(), 70m, 10m, new DateOnly(2024, 3, 1));
            fixture.AssertOnlySubPotsSaved(3);
        });
    }

    private static SavingsSubPot Pot(string name, decimal amount, decimal rate, DateOnly? month, Guid? id = null)
        => new() { Id = id ?? Guid.NewGuid(), Name = name, Amount = amount, MonthlyContribution = rate, LastContributionMonth = month };

    private static string OtherData(FinanceStateService state)
        => JsonSerializer.Serialize(new
        {
            state.BankBalance,
            state.SavingsPots,
            state.CreditCards,
            state.DirectDebits,
            state.BudgetCategories,
            state.UpcomingCosts,
            state.OneOffPayments,
            state.Incomes
        });
}
