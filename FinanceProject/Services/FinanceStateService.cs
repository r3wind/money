using System.Text.Json;
using FinanceProject.Models;
using Microsoft.JSInterop;

namespace FinanceProject.Services;

public class FinanceStateService
{
    private readonly IJSRuntime _js;

    public List<DirectDebit> DirectDebits { get; private set; } = [];
    public List<BudgetCategory> BudgetCategories { get; private set; } = [];
    public List<UpcomingCost> UpcomingCosts { get; private set; } = [];

    public FinanceStateService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task LoadAsync()
    {
        DirectDebits = await LoadFromStorage<List<DirectDebit>>("finance_directdebits") ?? [];
        BudgetCategories = await LoadFromStorage<List<BudgetCategory>>("finance_budgetcategories") ?? [];
        UpcomingCosts = await LoadFromStorage<List<UpcomingCost>>("finance_upcomingcosts") ?? [];
    }

    // Direct Debits
    public async Task AddDirectDebitAsync(DirectDebit item)
    {
        DirectDebits.Add(item);
        await SaveDirectDebitsAsync();
    }

    public async Task UpdateDirectDebitAsync(DirectDebit item)
    {
        var idx = DirectDebits.FindIndex(d => d.Id == item.Id);
        if (idx >= 0) DirectDebits[idx] = item;
        await SaveDirectDebitsAsync();
    }

    public async Task RemoveDirectDebitAsync(Guid id)
    {
        DirectDebits.RemoveAll(d => d.Id == id);
        await SaveDirectDebitsAsync();
    }

    // Budget Categories
    public async Task AddBudgetCategoryAsync(BudgetCategory item)
    {
        BudgetCategories.Add(item);
        await SaveBudgetCategoriesAsync();
    }

    public async Task UpdateBudgetCategoryAsync(BudgetCategory item)
    {
        var idx = BudgetCategories.FindIndex(b => b.Id == item.Id);
        if (idx >= 0) BudgetCategories[idx] = item;
        await SaveBudgetCategoriesAsync();
    }

    public async Task RemoveBudgetCategoryAsync(Guid id)
    {
        BudgetCategories.RemoveAll(b => b.Id == id);
        await SaveBudgetCategoriesAsync();
    }

    // Upcoming Costs
    public async Task AddUpcomingCostAsync(UpcomingCost item)
    {
        UpcomingCosts.Add(item);
        await SaveUpcomingCostsAsync();
    }

    public async Task UpdateUpcomingCostAsync(UpcomingCost item)
    {
        var idx = UpcomingCosts.FindIndex(u => u.Id == item.Id);
        if (idx >= 0) UpcomingCosts[idx] = item;
        await SaveUpcomingCostsAsync();
    }

    public async Task RemoveUpcomingCostAsync(Guid id)
    {
        UpcomingCosts.RemoveAll(u => u.Id == id);
        await SaveUpcomingCostsAsync();
    }

    // Dashboard calculations
    public decimal TotalMonthlyDirectDebits()
        => DirectDebits.Sum(d => d.Amount);

    public decimal RemainingMonthDirectDebits()
    {
        var today = DateTime.Today;
        return DirectDebits
            .Where(d => d.DayOfMonth >= today.Day)
            .Sum(d => d.Amount);
    }

    public decimal RemainingMonthTotal()
    {
        var today = DateTime.Today;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var remainingDays = daysInMonth - today.Day + 1;
        var fraction = (decimal)remainingDays / daysInMonth;

        var remainingBudget = BudgetCategories.Sum(b => b.MonthlyAmount) * fraction;
        return RemainingMonthDirectDebits() + remainingBudget;
    }

    private async Task SaveDirectDebitsAsync()
        => await SaveToStorage("finance_directdebits", DirectDebits);

    private async Task SaveBudgetCategoriesAsync()
        => await SaveToStorage("finance_budgetcategories", BudgetCategories);

    private async Task SaveUpcomingCostsAsync()
        => await SaveToStorage("finance_upcomingcosts", UpcomingCosts);

    private async Task SaveToStorage<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        await _js.InvokeVoidAsync("localStorage.setItem", key, json);
    }

    private async Task<T?> LoadFromStorage<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        if (string.IsNullOrEmpty(json)) return default;
        return JsonSerializer.Deserialize<T>(json);
    }
}
