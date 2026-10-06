using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FinanceProject.Models;
using FinanceProject.Services;

namespace FinanceProject.Tests;

internal static class UpcomingCostRegressionTests
{
    private const string CostsKey = "finance_upcomingcosts";

    public static IEnumerable<RegressionCase> Cases()
    {
        yield return RegressionCase.Sync("Upcoming links: legacy model defaults remain empty and unlinked", () =>
        {
            Assert.Equal(Guid.Empty, new SavingsSubPot().Id, "New model leaves identity initialization to state");
            var pot = JsonSerializer.Deserialize<SavingsSubPot>("{\"Name\":\"Legacy\",\"Amount\":12.34}")!;
            Assert.Equal(Guid.Empty, pot.Id, "Legacy JSON has no generated model identity");
            Assert.Pot(pot, 12.34m, 0m, null);
            var cost = JsonSerializer.Deserialize<UpcomingCost>("{\"Name\":\"Legacy cost\",\"Amount\":50,\"Date\":\"2024-01-20T00:00:00\"}")!;
            Assert.Equal<Guid?>(null, new UpcomingCost().SavingsPotId, "New cost defaults unlinked");
            Assert.Equal<Guid?>(null, cost.SavingsPotId, "Legacy JSON defaults unlinked");
            Assert.Equal(50m, cost.Amount, "Legacy cost amount unchanged");
            Assert.Equal(new DateTime(2024, 1, 20), cost.Date, "Legacy cost date unchanged");
            var fixture = new StateFixture();
            Assert.Equal(50m, Calculator(fixture).GetUncoveredUpcomingCostAmount(cost), "Legacy cost remains fully payable");
        });
        yield return RegressionCase.Sync("Upcoming links: JSON preserves cost identity, link and monetary fields", () =>
        {
            var pot = Pot("Linked", 12.34m);
            var cost = Cost("Round-trip", 98.76m, new DateTime(2024, 2, 29), pot.Id);
            cost.Details = "Preserved details";
            var restoredPot = Copy(pot);
            var restoredCost = Copy(cost);
            Assert.Equal(JsonSerializer.Serialize(pot), JsonSerializer.Serialize(restoredPot), "Pot JSON round-trip");
            Assert.Equal(JsonSerializer.Serialize(cost), JsonSerializer.Serialize(restoredCost), "Cost JSON round-trip");
            Assert.Equal<Guid?>(restoredPot.Id, restoredCost.SavingsPotId, "Link resolves to round-tripped identity");
        });
        yield return new("Upcoming links: duplicate-name legacy pots migrate distinct stable IDs once", async () =>
        {
            var fixture = new StateFixture();
            fixture.Storage.Storage[StateFixture.SubPotsKey] = "[{\"Name\":\"Duplicate\",\"Amount\":12.34},{\"Name\":\"Duplicate\",\"Amount\":56.78}]";
            fixture.Storage.Storage[CostsKey] = "[{\"Name\":\"Legacy\",\"Amount\":90,\"Date\":\"2024-01-20T00:00:00\"}]";
            var legacyCosts = fixture.Storage.Storage[CostsKey];
            await fixture.State.LoadAsync();
            var pots = fixture.State.SavingsSubPots;
            Assert.Equal(2, pots.Count, "Duplicate names both retained");
            Assert.True(pots.All(p => p.Id != Guid.Empty), "All legacy IDs initialized");
            Assert.Equal(2, pots.Select(p => p.Id).Distinct().Count(), "Duplicate names get distinct identities");
            Assert.Pot(pots[0], 12.34m, 0m, null);
            Assert.Pot(pots[1], 56.78m, 0m, null);
            Assert.True(pots.All(p => p.Name == "Duplicate"), "Names unchanged");
            Assert.Equal<Guid?>(null, fixture.State.UpcomingCosts.Single().SavingsPotId, "Migration does not infer links by name");
            var payload = fixture.Storage.Storage[StateFixture.SubPotsKey];
            Assert.Equal(JsonSerializer.Serialize(pots), JsonSerializer.Serialize(fixture.SavedPots), "Migrated identities and money persisted");
            for (var i = 0; i < 3; i++)
            {
                var fresh = fixture.FreshState();
                await fresh.LoadAsync();
                Assert.Equal(JsonSerializer.Serialize(pots), JsonSerializer.Serialize(fresh.SavingsSubPots), "Fresh reload keeps IDs and balances");
            }
            Assert.Equal(payload, fixture.Storage.Storage[StateFixture.SubPotsKey], "No repeated migration writes");
            Assert.Equal(legacyCosts, fixture.Storage.Storage[CostsKey], "Legacy costs storage untouched");
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("Upcoming links: duplicate persisted IDs are repaired without changing valid identity", async () =>
        {
            var fixture = new StateFixture();
            var first = Pot("Duplicate", 10m);
            var second = Pot("Duplicate", 20m);
            second.Id = first.Id;
            fixture.Seed(first, second);
            await fixture.State.LoadAsync();
            Assert.Equal(first.Id, fixture.State.SavingsSubPots[0].Id, "First valid ID retained");
            Assert.True(fixture.State.SavingsSubPots[1].Id != Guid.Empty, "Replacement ID nonempty");
            Assert.True(fixture.State.SavingsSubPots[1].Id != first.Id, "Duplicate ID repaired");
            Assert.Pot(fixture.State.SavingsSubPots[0], 10m, 0m, null);
            Assert.Pot(fixture.State.SavingsSubPots[1], 20m, 0m, null);
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(JsonSerializer.Serialize(fixture.State.SavingsSubPots), JsonSerializer.Serialize(fresh.SavingsSubPots), "Repaired identities stable");
            fixture.AssertOnlySubPotsSaved(1);
        });
        yield return new("Upcoming links: state initializes and persists IDs on add", async () =>
        {
            var fixture = new StateFixture();
            var first = new SavingsSubPot { Name = "Same name", Amount = 10m };
            await fixture.State.AddSavingsSubPotAsync(first);
            Assert.True(first.Id != Guid.Empty, "State initializes empty ID");
            var second = new SavingsSubPot { Id = first.Id, Name = first.Name, Amount = 20m };
            await fixture.State.AddSavingsSubPotAsync(second);
            Assert.True(second.Id != Guid.Empty && second.Id != first.Id, "State repairs duplicate ID on add");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(JsonSerializer.Serialize(fixture.State.SavingsSubPots), JsonSerializer.Serialize(fresh.SavingsSubPots), "Added identities persisted");
            fixture.AssertOnlySubPotsSaved(2);
        });
        foreach (var (balance, expected, label) in new[]
        {
            (40m, 60m, "partial"), (100m, 0m, "full"), (150m, 0m, "excess"), (0m, 100m, "zero")
        })
        {
            yield return RegressionCase.Sync($"Upcoming coverage: {label} pot balance is netted without mutation", () =>
            {
                var fixture = new StateFixture();
                var pot = Pot(label, balance);
                var cost = Cost(label, 100m, new DateTime(2024, 1, 20), pot.Id);
                fixture.State.SavingsSubPots.Add(pot);
                fixture.State.UpcomingCosts.Add(cost);
                var before = JsonSerializer.Serialize(fixture.State);
                var calculator = Calculator(fixture);
                for (var i = 0; i < 3; i++)
                {
                    Assert.Equal(expected, calculator.GetUncoveredUpcomingCostAmount(cost), "Uncovered cost");
                    Assert.Equal(expected, calculator.UpcomingCostsThisMonth, "Monthly net cost");
                }
                Assert.Equal(before, JsonSerializer.Serialize(fixture.State), "Calculations do not spend pot or alter original cost");
                Assert.Equal(0, fixture.Storage.SavedKeys.Count, "Calculations never persist");
            });
        }
        yield return RegressionCase.Sync("Upcoming coverage: unlinked, empty and missing pot use original cost", () =>
        {
            var fixture = new StateFixture();
            fixture.State.SavingsSubPots.Add(Pot("Unrelated", 1000m));
            var calculator = Calculator(fixture);
            foreach (var id in new Guid?[] { null, Guid.Empty, Guid.NewGuid() })
            {
                var cost = Cost("No matching pot", 123.45m, new DateTime(2024, 1, 20), id);
                Assert.Equal(123.45m, calculator.GetUncoveredUpcomingCostAmount(cost), "Unmatched link uses original amount");
                Assert.Equal(123.45m, cost.Amount, "Original amount retained");
                Assert.Equal(id, cost.SavingsPotId, "Calculation does not repair links");
            }
        });
        yield return new("Upcoming coverage: separate costs use only their own pot even with duplicate names", async () =>
        {
            var fixture = new StateFixture();
            var first = Pot("Duplicate", 40m);
            var second = Pot("Duplicate", 120m);
            await fixture.State.AddSavingsSubPotAsync(first);
            await fixture.State.AddSavingsSubPotAsync(second);
            await fixture.State.AddUpcomingCostAsync(Cost("First", 100m, new DateTime(2024, 1, 20), first.Id));
            await fixture.State.AddUpcomingCostAsync(Cost("Second", 100m, new DateTime(2024, 1, 21), second.Id));
            await fixture.State.AddUpcomingCostAsync(Cost("Unlinked", 25m, new DateTime(2024, 1, 22)));
            var before = JsonSerializer.Serialize(fixture.State);
            var calculator = Calculator(fixture);
            Assert.Equal(60m, calculator.GetUncoveredUpcomingCostAmount(fixture.State.UpcomingCosts[0]), "First pot only");
            Assert.Equal(0m, calculator.GetUncoveredUpcomingCostAmount(fixture.State.UpcomingCosts[1]), "Second pot covers its own cost");
            Assert.Equal(85m, calculator.UpcomingCostsThisMonth, "Excess balance never covers unlinked cost");
            Assert.Equal(before, JsonSerializer.Serialize(fixture.State), "Multiple calculations do not mutate state");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(before, JsonSerializer.Serialize(fresh), "Links and monetary data survive storage reload");
            Assert.Equal(85m, new FinanceCalculator(fresh, fixture.Clock).UpcomingCostsThisMonth, "Reload resolves links by ID");
        });
        yield return new("Upcoming links: add and update exclusivity failures leave state and storage unchanged", async () =>
        {
            var fixture = new StateFixture();
            var pot = Pot("Exclusive", 30m);
            await fixture.State.AddSavingsSubPotAsync(pot);
            var owner = Cost("Owner", 100m, new DateTime(2024, 1, 20), pot.Id);
            var other = Cost("Other", 200m, new DateTime(2024, 1, 21));
            await fixture.State.AddUpcomingCostAsync(owner);
            await fixture.State.AddUpcomingCostAsync(other);
            await RejectWithoutChanges(fixture, () => fixture.State.AddUpcomingCostAsync(Cost("Conflict", 5m, owner.Date, pot.Id)));
            var edited = Copy(other);
            edited.Name = "Rejected edit";
            edited.Amount = 999m;
            edited.SavingsPotId = pot.Id;
            await RejectWithoutChanges(fixture, () => fixture.State.UpdateUpcomingCostAsync(edited));
        });
        foreach (var empty in new[] { false, true })
        {
            yield return new($"Upcoming links: {(empty ? "empty" : "missing")} pot rejected on add and update without writes", async () =>
            {
                var fixture = new StateFixture();
                await fixture.State.AddSavingsSubPotAsync(Pot("Existing", 10m));
                var cost = Cost("Existing cost", 100m, new DateTime(2024, 1, 20));
                await fixture.State.AddUpcomingCostAsync(cost);
                var invalidId = empty ? Guid.Empty : Guid.NewGuid();
                await RejectWithoutChanges(fixture, () => fixture.State.AddUpcomingCostAsync(Cost("Invalid", 20m, cost.Date, invalidId)));
                var edited = Copy(cost);
                edited.SavingsPotId = invalidId;
                edited.Amount = 999m;
                await RejectWithoutChanges(fixture, () => fixture.State.UpdateUpcomingCostAsync(edited));
            });
        }
        yield return new("Upcoming links: retaining, switching, unlinking and deleting costs release pots", async () =>
        {
            var fixture = new StateFixture();
            var first = Pot("First", 40m);
            var second = Pot("Second", 70m);
            await fixture.State.AddSavingsSubPotAsync(first);
            await fixture.State.AddSavingsSubPotAsync(second);
            var cost = Cost("Owner", 100m, new DateTime(2024, 1, 20), first.Id);
            await fixture.State.AddUpcomingCostAsync(cost);
            var retained = Copy(cost);
            retained.Amount = 120m;
            await fixture.State.UpdateUpcomingCostAsync(retained);
            Assert.Equal(1, fixture.State.UpcomingCosts.Count, "Retaining link updates rather than duplicates");
            Assert.Equal(80m, Calculator(fixture).GetUncoveredUpcomingCostAmount(retained), "Same cost can retain its pot");
            await AssertReloadedCosts(fixture);
            var switched = Copy(retained);
            switched.SavingsPotId = second.Id;
            await fixture.State.UpdateUpcomingCostAsync(switched);
            Assert.Equal(50m, Calculator(fixture).GetUncoveredUpcomingCostAmount(switched), "Switch uses new balance");
            var newOwner = Cost("Uses freed first", 60m, cost.Date, first.Id);
            await fixture.State.AddUpcomingCostAsync(newOwner);
            await AssertReloadedCosts(fixture);
            var unlinked = Copy(switched);
            unlinked.SavingsPotId = null;
            await fixture.State.UpdateUpcomingCostAsync(unlinked);
            Assert.Equal(120m, Calculator(fixture).GetUncoveredUpcomingCostAmount(unlinked), "Unlink restores full cost");
            await fixture.State.AddUpcomingCostAsync(Cost("Uses freed second", 80m, cost.Date, second.Id));
            await AssertReloadedCosts(fixture);
            await fixture.State.RemoveUpcomingCostAsync(newOwner.Id);
            Assert.False(fixture.State.UpcomingCosts.Any(c => c.Id == newOwner.Id), "Deleted cost gone");
            await fixture.State.AddUpcomingCostAsync(Cost("Reuses deleted link", 50m, cost.Date, first.Id));
            await AssertReloadedCosts(fixture);
            Assert.Equal(3, fixture.State.UpcomingCosts.Count, "Final collection has no deleted cost");
        });
        yield return new("Upcoming links: ID and legacy-name edits preserve links and dynamic coverage across reload", async () =>
        {
            var fixture = new StateFixture();
            var pot = Pot("Original", 20m);
            var other = Pot("Other", 10m);
            await fixture.State.AddSavingsSubPotAsync(pot);
            await fixture.State.AddSavingsSubPotAsync(other);
            var cost = Cost("Linked", 100m, new DateTime(2024, 1, 20), pot.Id);
            await fixture.State.AddUpcomingCostAsync(cost);
            var calculator = Calculator(fixture);
            Assert.Equal(80m, calculator.GetUncoveredUpcomingCostAmount(cost), "Initial coverage");
            await fixture.State.UpdateSavingsSubPotAsync(new SavingsSubPot { Id = pot.Id, Name = "ID rename", Amount = 50m }, originalName: other.Name);
            Assert.Equal(pot.Id, fixture.State.SavingsSubPots.Single(p => p.Name == "ID rename").Id, "ID lookup takes precedence over name");
            Assert.Equal(10m, fixture.State.SavingsSubPots.Single(p => p.Id == other.Id).Amount, "Other pot untouched");
            Assert.Equal(50m, calculator.GetUncoveredUpcomingCostAmount(cost), "Existing calculator uses updated balance");
            var legacyEdit = new SavingsSubPot { Name = "Name fallback rename", Amount = 80m };
            await fixture.State.UpdateSavingsSubPotAsync(legacyEdit, originalName: "ID rename");
            Assert.Equal(pot.Id, legacyEdit.Id, "Empty-ID rename inherits original identity");
            Assert.Equal(20m, calculator.GetUncoveredUpcomingCostAmount(cost), "Fallback edit dynamically recalculates");
            await fixture.State.UpdateSavingsSubPotAsync(new SavingsSubPot { Name = legacyEdit.Name, Amount = 120m });
            Assert.Equal(0m, calculator.GetUncoveredUpcomingCostAmount(cost), "Empty-ID same-name amount edit covers cost");
            Assert.Equal<Guid?>(pot.Id, fixture.State.UpcomingCosts.Single().SavingsPotId, "All edits retain link");
            Assert.Equal(100m, fixture.State.UpcomingCosts.Single().Amount, "Original cost retained");
            Assert.Equal(2, fixture.State.SavingsSubPots.Count, "Edits do not add pots");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(pot.Id, fresh.SavingsSubPots.Single(p => p.Name == legacyEdit.Name).Id, "Edited ID persists");
            Assert.Equal<Guid?>(pot.Id, fresh.UpcomingCosts.Single().SavingsPotId, "Link persists after rename");
            Assert.Equal(0m, new FinanceCalculator(fresh, fixture.Clock).GetUncoveredUpcomingCostAmount(fresh.UpcomingCosts.Single()), "Reload uses edited balance");
        });
        yield return new("Upcoming links: removing a pot clears only its links and persists full cost", async () =>
        {
            var fixture = new StateFixture();
            var removed = Pot("Removed", 70m);
            var retained = Pot("Retained", 40m);
            await fixture.State.AddSavingsSubPotAsync(removed);
            await fixture.State.AddSavingsSubPotAsync(retained);
            var cost = Cost("Affected", 100m, new DateTime(2024, 1, 20), removed.Id);
            var otherCost = Cost("Unaffected", 80m, cost.Date, retained.Id);
            await fixture.State.AddUpcomingCostAsync(cost);
            await fixture.State.AddUpcomingCostAsync(otherCost);
            var staleCost = Copy(cost);
            var otherBefore = JsonSerializer.Serialize(otherCost);
            var calculator = Calculator(fixture);
            Assert.Equal(30m, calculator.GetUncoveredUpcomingCostAmount(cost), "Coverage before removal");
            var saves = fixture.Storage.SavedKeys.Count;
            await fixture.State.RemoveSavingsSubPotAsync(removed.Name);
            Assert.Equal(saves + 2, fixture.Storage.SavedKeys.Count, "Removal saves pots and affected costs once each");
            Assert.Equal(StateFixture.SubPotsKey, fixture.Storage.SavedKeys[saves], "Pot removal persisted");
            Assert.Equal(CostsKey, fixture.Storage.SavedKeys[saves + 1], "Cleared links persisted");
            Assert.Equal<Guid?>(null, cost.SavingsPotId, "Removed link cleared in memory");
            Assert.Equal(100m, calculator.GetUncoveredUpcomingCostAmount(cost), "Cleared link restores full cost");
            Assert.Equal(100m, calculator.GetUncoveredUpcomingCostAmount(staleCost), "Stale missing-pot link also restores full cost");
            Assert.Equal(otherBefore, JsonSerializer.Serialize(otherCost), "Unrelated linked cost unchanged");
            Assert.Equal(40m, calculator.GetUncoveredUpcomingCostAmount(otherCost), "Other pot still covers its cost");
            var saved = fixture.Storage.Read<List<UpcomingCost>>(CostsKey).Single(c => c.Id == cost.Id);
            Assert.Equal<Guid?>(null, saved.SavingsPotId, "Cleared link stored as unlinked");
            Assert.Equal(100m, saved.Amount, "Persisted original cost unchanged");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            Assert.Equal(1, fresh.SavingsSubPots.Count, "Removed pot stays removed");
            Assert.Equal(retained.Id, fresh.SavingsSubPots.Single().Id, "Other pot identity retained");
            Assert.Equal<Guid?>(null, fresh.UpcomingCosts.Single(c => c.Id == cost.Id).SavingsPotId, "Reload keeps cleared link");
            Assert.Equal(140m, new FinanceCalculator(fresh, fixture.Clock).UpcomingCostsThisMonth, "Reload sums original and unaffected net cost");
            Assert.Equal(saves + 2, fixture.Storage.SavedKeys.Count, "Reload introduces no writes");
        });
        yield return new("Upcoming coverage: monthly contributions dynamically reduce uncovered cost on reload", async () =>
        {
            var fixture = new StateFixture();
            var pot = Pot("Contributing", 20m);
            pot.MonthlyContribution = 30m;
            await fixture.State.AddSavingsSubPotAsync(pot);
            var cost = Cost("Future", 100m, new DateTime(2024, 6, 20), pot.Id);
            await fixture.State.AddUpcomingCostAsync(cost);
            var calculator = Calculator(fixture);
            Assert.Equal(80m, calculator.GetUncoveredUpcomingCostAmount(cost), "Initial uncovered amount");
            fixture.Clock.Set(2024, 2, 1);
            await fixture.State.LoadAsync();
            Assert.Equal(50m, calculator.GetUncoveredUpcomingCostAmount(fixture.State.UpcomingCosts.Single()), "Existing calculator sees applied contribution");
            Assert.Pot(fixture.State.SavingsSubPots.Single(), 50m, 30m, new DateOnly(2024, 2, 1));
            fixture.Clock.Set(2024, 4, 1);
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            var before = JsonSerializer.Serialize(fresh);
            Assert.Pot(fresh.SavingsSubPots.Single(), 110m, 30m, new DateOnly(2024, 4, 1));
            Assert.Equal(0m, new FinanceCalculator(fresh, fixture.Clock).GetUncoveredUpcomingCostAmount(fresh.UpcomingCosts.Single()), "Catch-up covers cost without negative amount");
            Assert.Equal(100m, fresh.UpcomingCosts.Single().Amount, "Contributions never change original cost");
            Assert.Equal<Guid?>(pot.Id, fresh.UpcomingCosts.Single().SavingsPotId, "Contribution reload retains link");
            Assert.Equal(before, JsonSerializer.Serialize(fresh), "Calculation does not allocate further contributions");
            var saves = fixture.Storage.SavedKeys.Count;
            await fixture.FreshState().LoadAsync();
            Assert.Equal(saves, fixture.Storage.SavedKeys.Count, "Repeated reload does not duplicate contributions");
        });
    }

    public static IEnumerable<RegressionCase> ForecastCases()
    {
        yield return new("Upcoming forecasts: current and next month sum net costs at clock boundaries", async () =>
        {
            var fixture = await ForecastFixture();
            var before = JsonSerializer.Serialize(fixture.State);
            var calculator = Calculator(fixture);
            Assert.Equal(80m, calculator.UpcomingCostsThisMonth, "Current month is 60 linked plus 20 unlinked, excluding today and past dates");
            Assert.Equal(45m, calculator.UpcomingCostsNextMonth, "January is 30 linked plus 15 unlinked, excluding other years");
            Assert.Equal(40m, calculator.OneOffIncomingThisMonth, "One-off incoming includes today but not yesterday");
            Assert.Equal(30m, calculator.OneOffIncomingNextMonth, "January incoming");
            Assert.Equal(60m, calculator.RemainingDirectDebits, "Only later direct debit remains");
            Assert.Equal(170m, calculator.ProRatedBudget, "December 15 includes 17 of 31 budget days");
            Assert.Equal(before, JsonSerializer.Serialize(fixture.State), "Monthly totals do not consume balances");
        });
        yield return new("Upcoming forecasts: end of month with and without income deducts uncovered costs only", async () =>
        {
            var fixture = await ForecastFixture();
            var calculator = Calculator(fixture);
            Assert.Equal(880m, calculator.EndOfMonthBalance, "1000 - 60 - 170 - 80 + 40 + 200 - 50");
            Assert.Equal(640m, calculator.EndOfMonthBalanceExcIncome, "1000 - 60 - 170 - 80 - 50");
            Assert.Equal(240m, calculator.EndOfMonthBalance - calculator.EndOfMonthBalanceExcIncome, "Income variant adds only unpaid and one-off income");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            var restored = new FinanceCalculator(fresh, fixture.Clock);
            Assert.Equal(880m, restored.EndOfMonthBalance, "Income forecast survives link reload");
            Assert.Equal(640m, restored.EndOfMonthBalanceExcIncome, "No-income forecast survives link reload");
        });
        yield return new("Upcoming forecasts: next month income toggle nets both monthly upcoming sums", async () =>
        {
            var fixture = await ForecastFixture();
            var calculator = Calculator(fixture);
            Assert.Equal(1010m, calculator.AllocatedSavings, "All linked and unused allocations");
            Assert.Equal(250m, calculator.UnallocatedSavings, "Only unallocated account money enters forecast");
            Assert.Equal(695m, calculator.NextMonthForecast(false), "1000 - 60 - 170 - 80 - 110 - 310 - 45 - 50 + 40 + 30 + 200 + 250");
            Assert.Equal(1195m, calculator.NextMonthForecast(true), "Including next income adds 500, without counting paid income twice");
            Assert.Equal(500m, calculator.NextMonthForecast(true) - calculator.NextMonthForecast(false), "Toggle adds exactly total next-month income");
        });
        yield return new("Upcoming forecasts: five-month loops net every cost without repeated savings or mutation", async () =>
        {
            var fixture = await ForecastFixture();
            var calculator = Calculator(fixture);
            var before = JsonSerializer.Serialize(fixture.State);
            var storageBefore = JsonSerializer.Serialize(fixture.Storage.Storage.OrderBy(pair => pair.Key));
            var saves = fixture.Storage.SavedKeys.Count;
            Assert.Equal(0, calculator.GetFutureForecast(0).Count, "Zero horizon is empty");
            Assert.Equal(0, calculator.GetFutureForecast(-1).Count, "Negative horizon is empty");
            var expectedBalances = new[] { 695m, 715m, 790m, 810m, 870m };
            var today = fixture.Clock.GetLocalNow().Date;
            foreach (var horizon in new[] { 1, 2, 5 })
            {
                var forecast = calculator.GetFutureForecast(horizon);
                Assert.Equal(horizon, forecast.Count, "Requested number of future months");
                for (var i = 0; i < horizon; i++)
                {
                    Assert.Equal(today.AddMonths(i + 1).ToString("MMM yyyy"), forecast[i].Label, "Label follows deterministic clock across year boundary");
                    Assert.Equal(expectedBalances[i], forecast[i].Balance, "Net forecast balance for offset " + (i + 1));
                }
                Assert.Equal(calculator.NextMonthForecast(false), forecast[0].Balance, "First forecast excludes next income");
            }
            Assert.Equal(before, JsonSerializer.Serialize(fixture.State), "All forecast branches leave pots, costs and other state unchanged");
            Assert.Equal(storageBefore, JsonSerializer.Serialize(fixture.Storage.Storage.OrderBy(pair => pair.Key)), "Forecasts do not change stored data");
            Assert.Equal(saves, fixture.Storage.SavedKeys.Count, "Forecasts perform no writes");
            var unused = Copy(fixture.State.SavingsSubPots.Single(p => p.Name == "Unused"));
            unused.Amount += 100m;
            await fixture.State.UpdateSavingsSubPotAsync(unused);
            var reduced = calculator.GetFutureForecast(5);
            for (var i = 0; i < reduced.Count; i++)
                Assert.Equal(expectedBalances[i] - 100m, reduced[i].Balance, "Extra unused allocation reduces unallocated savings once, not once per loop");
            var account = Copy(fixture.State.SavingsPots.Single());
            account.Amount += 100m;
            await fixture.State.UpdateSavingsPotAsync(account);
            var restored = calculator.GetFutureForecast(5);
            for (var i = 0; i < restored.Count; i++)
                Assert.Equal(expectedBalances[i], restored[i].Balance, "Fully allocated extra savings never enter forecast as available cash");
            var fresh = fixture.FreshState();
            await fresh.LoadAsync();
            var reloaded = new FinanceCalculator(fresh, fixture.Clock).GetFutureForecast(5);
            for (var i = 0; i < reloaded.Count; i++)
                Assert.Equal(expectedBalances[i], reloaded[i].Balance, "Long forecast remains net after storage reload");
        });
    }

    private static async Task<StateFixture> ForecastFixture()
    {
        var fixture = new StateFixture(2024, 12, 15);
        var today = fixture.Clock.GetLocalNow().Date;
        var pots = new[]
        {
            Pot("December", 40m), Pot("January", 70m), Pot("February", 80m),
            Pot("March", 120m), Pot("April", 200m), Pot("Unused", 500m)
        };
        fixture.Seed(pots);
        fixture.Storage.Seed("finance_bankbalance", 1000m);
        fixture.Storage.Seed("finance_savingspots", new[] { new SavingsPot { Name = "Account", Amount = 1260m } });
        fixture.Storage.Seed("finance_incomes", new[]
        {
            new Income { Name = "Unpaid", Amount = 200m },
            new Income { Name = "Paid", Amount = 300m, PaidThisMonth = true }
        });
        fixture.Storage.Seed("finance_directdebits", new[]
        {
            new DirectDebit { Name = "Earlier", Amount = 40m, DayOfMonth = 1 },
            new DirectDebit { Name = "Today", Amount = 10m, DayOfMonth = today.Day },
            new DirectDebit { Name = "Later", Amount = 60m, DayOfMonth = 20 }
        });
        fixture.Storage.Seed("finance_budgetcategories", new[] { new BudgetCategory { Name = "Budget", MonthlyAmount = 310m } });
        fixture.Storage.Seed("finance_creditcards", new[] { new CreditCard { Name = "Card", Limit = 100m, AvailableCredit = 50m } });
        fixture.Storage.Seed("finance_oneoffpayments", new[]
        {
            new OneOffPayment { Name = "Yesterday", Amount = 999m, Date = today.AddDays(-1) },
            new OneOffPayment { Name = "Today", Amount = 25m, Date = today },
            new OneOffPayment { Name = "Later", Amount = 15m, Date = today.AddDays(1) },
            new OneOffPayment { Name = "January", Amount = 30m, Date = new DateTime(2025, 1, 1) },
            new OneOffPayment { Name = "February", Amount = 20m, Date = new DateTime(2025, 2, 1) }
        });
        await fixture.State.LoadAsync();
        var costs = new[]
        {
            Cost("Yesterday excluded", 999m, today.AddDays(-1)),
            Cost("Today excluded", 999m, today),
            Cost("Old December excluded", 999m, today.AddYears(-1).AddDays(1)),
            Cost("December linked", 100m, today.AddDays(1), pots[0].Id),
            Cost("December unlinked", 20m, new DateTime(2024, 12, 31)),
            Cost("January linked", 100m, new DateTime(2025, 1, 1), pots[1].Id),
            Cost("January unlinked", 15m, new DateTime(2025, 1, 31)),
            Cost("Other January excluded", 999m, new DateTime(2026, 1, 1)),
            Cost("February linked", 150m, new DateTime(2025, 2, 2), pots[2].Id),
            Cost("February unlinked", 10m, new DateTime(2025, 2, 28)),
            Cost("March overcovered", 100m, new DateTime(2025, 3, 3), pots[3].Id),
            Cost("March unlinked", 5m, new DateTime(2025, 3, 31)),
            Cost("April linked", 250m, new DateTime(2025, 4, 4), pots[4].Id),
            Cost("April unlinked", 10m, new DateTime(2025, 4, 30)),
            Cost("May unlinked", 20m, new DateTime(2025, 5, 1))
        };
        foreach (var cost in costs)
            await fixture.State.AddUpcomingCostAsync(cost);
        return fixture;
    }

    private static SavingsSubPot Pot(string name, decimal amount)
        => new() { Id = Guid.NewGuid(), Name = name, Amount = amount };

    private static UpcomingCost Cost(string name, decimal amount, DateTime date, Guid? potId = null)
        => new() { Name = name, Amount = amount, Date = date, SavingsPotId = potId };

    private static FinanceCalculator Calculator(StateFixture fixture) => new(fixture.State, fixture.Clock);

    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static async Task AssertReloadedCosts(StateFixture fixture)
    {
        var fresh = fixture.FreshState();
        await fresh.LoadAsync();
        Assert.Equal(JsonSerializer.Serialize(fixture.State.UpcomingCosts), JsonSerializer.Serialize(fresh.UpcomingCosts), "Cost identities and links survive fresh reload");
        Assert.Equal(JsonSerializer.Serialize(fixture.State.SavingsSubPots), JsonSerializer.Serialize(fresh.SavingsSubPots), "Lifecycle operations leave pots unchanged");
    }

    private static async Task RejectWithoutChanges(StateFixture fixture, Func<Task> operation)
    {
        var state = JsonSerializer.Serialize(fixture.State);
        var storage = JsonSerializer.Serialize(fixture.Storage.Storage.OrderBy(pair => pair.Key));
        var saves = JsonSerializer.Serialize(fixture.Storage.SavedKeys);
        var rejected = false;
        try
        {
            await operation();
        }
        catch (ValidationException)
        {
            rejected = true;
        }
        Assert.True(rejected, "Invalid link must throw ValidationException");
        Assert.Equal(state, JsonSerializer.Serialize(fixture.State), "Rejected operation leaves all in-memory state unchanged");
        Assert.Equal(storage, JsonSerializer.Serialize(fixture.Storage.Storage.OrderBy(pair => pair.Key)), "Rejected operation leaves persisted payloads unchanged");
        Assert.Equal(saves, JsonSerializer.Serialize(fixture.Storage.SavedKeys), "Rejected operation makes no storage writes");
    }
}
