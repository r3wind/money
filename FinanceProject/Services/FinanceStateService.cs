using FinanceProject.Models;

namespace FinanceProject.Services;

public class FinanceStateService
{
    private readonly FirestoreService _firestore;

    public List<DirectDebit> DirectDebits { get; private set; } = [];
    public List<BudgetCategory> BudgetCategories { get; private set; } = [];
    public List<UpcomingCost> UpcomingCosts { get; private set; } = [];
    public List<OneOffPayment> OneOffPayments { get; private set; } = [];
    public List<Income> Incomes { get; private set; } = [];
    public List<SavingsPot> SavingsPots { get; private set; } = [];
    public List<SavingsSubPot> SavingsSubPots { get; private set; } = [];
    public List<CreditCard> CreditCards { get; private set; } = [];

    public IEnumerable<SavingsPot> RegularSavingsPots()
    {
        return SavingsPots;
    }
    public decimal BankBalance { get; private set; }

    public FinanceStateService(FirestoreService firestore)
    {
        _firestore = firestore;
    }

    public async Task LoadAsync()
    {
        DirectDebits = await LoadFromStorage<List<DirectDebit>>("finance_directdebits") ?? [];
        BudgetCategories = await LoadFromStorage<List<BudgetCategory>>("finance_budgetcategories") ?? [];
        UpcomingCosts = await LoadFromStorage<List<UpcomingCost>>("finance_upcomingcosts") ?? [];
        OneOffPayments = await LoadFromStorage<List<OneOffPayment>>("finance_oneoffpayments") ?? [];
        Incomes = await LoadFromStorage<List<Income>>("finance_incomes") ?? [];
        SavingsPots = await LoadFromStorage<List<SavingsPot>>("finance_savingspots") ?? [];
        SavingsSubPots = await LoadFromStorage<List<SavingsSubPot>>("finance_savingssubpots") ?? [];
        CreditCards = await LoadFromStorage<List<CreditCard>>("finance_creditcards") ?? [];
        BankBalance = await LoadFromStorage<decimal>("finance_bankbalance");
    }

    public async Task SetBankBalanceAsync(decimal amount)
    {
        BankBalance = amount;
        await SaveToStorage("finance_bankbalance", BankBalance);
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

    // One-Off Incoming Payments
    public async Task AddOneOffPaymentAsync(OneOffPayment item)
    {
        OneOffPayments.Add(item);
        await SaveOneOffPaymentsAsync();
    }

    public async Task UpdateOneOffPaymentAsync(OneOffPayment item)
    {
        var idx = OneOffPayments.FindIndex(p => p.Id == item.Id);
        if (idx >= 0) OneOffPayments[idx] = item;
        await SaveOneOffPaymentsAsync();
    }

    public async Task RemoveOneOffPaymentAsync(Guid id)
    {
        OneOffPayments.RemoveAll(p => p.Id == id);
        await SaveOneOffPaymentsAsync();
    }

    // Savings Pots
    public async Task AddSavingsPotAsync(SavingsPot pot)
    {
        SavingsPots.Add(pot);
        await SaveSavingsPotsAsync();
    }

    public async Task UpdateSavingsPotAsync(SavingsPot pot)
    {
        var idx = SavingsPots.FindIndex(p => p.Name == pot.Name);
        if (idx >= 0) SavingsPots[idx] = pot;
        await SaveSavingsPotsAsync();
    }

    public async Task RemoveSavingsPotAsync(string name)
    {
        SavingsPots.RemoveAll(s => s.Name == name);
        await SaveSavingsPotsAsync();
    }

    private async Task SaveSavingsPotsAsync()
    {
        await SaveToStorage("finance_savingspots", SavingsPots);
    }

    // Savings Categories
    public async Task AddSavingsSubPotAsync(SavingsSubPot pot)
    {
        SavingsSubPots.Add(pot);
        await SaveSavingsSubPotsAsync();
    }

    public async Task UpdateSavingsSubPotAsync(SavingsSubPot pot)
    {
        var idx = SavingsSubPots.FindIndex(p => p.Name == pot.Name);
        if (idx >= 0) SavingsSubPots[idx] = pot;
        await SaveSavingsSubPotsAsync();
    }

    public async Task RemoveSavingsSubPotAsync(string name)
    {
        SavingsSubPots.RemoveAll(p => p.Name == name);
        await SaveSavingsSubPotsAsync();
    }

    private async Task SaveSavingsSubPotsAsync()
    {
        await SaveToStorage("finance_savingssubpots", SavingsSubPots);
    }

    // Credit Cards
    public async Task AddCreditCardAsync(CreditCard card)
    {
        CreditCards.Add(card);
        await SaveCreditCardsAsync();
    }

    public async Task UpdateCreditCardAsync(CreditCard card)
    {
        var idx = CreditCards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) CreditCards[idx] = card;
        await SaveCreditCardsAsync();
    }

    public async Task RemoveCreditCardAsync(Guid id)
    {
        CreditCards.RemoveAll(c => c.Id == id);
        await SaveCreditCardsAsync();
    }

    private async Task SaveCreditCardsAsync()
        => await SaveToStorage("finance_creditcards", CreditCards);

    public decimal TotalCreditCardBalance()
        => CreditCards.Sum(c => c.Balance);

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

    public decimal ProRatedBudget()
    {
        var today = DateTime.Today;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var remainingDays = daysInMonth - today.Day + 1;
        var fraction = (decimal)remainingDays / daysInMonth;
        return BudgetCategories.Sum(b => b.MonthlyAmount) * fraction;
    }

    public decimal UnpaidIncome()
        => Incomes.Where(i => !i.PaidThisMonth).Sum(i => i.Amount);

    public decimal RemainingOneOffIncomingPayments()
    {
        var today = DateTime.Today;
        return OneOffPayments
            .Where(p => p.Date >= today && p.Date.Year == today.Year && p.Date.Month == today.Month)
            .Sum(p => p.Amount);
    }

    public decimal NextMonthOneOffIncomingPayments()
    {
        var nextMonth = DateTime.Today.AddMonths(1);
        return OneOffPayments
            .Where(p => p.Date.Year == nextMonth.Year && p.Date.Month == nextMonth.Month)
            .Sum(p => p.Amount);
    }

    public decimal CurrentAndNextMonthOneOffIncomingPayments()
    {
        var today = DateTime.Today;
        var nextMonth = today.AddMonths(1);
        return OneOffPayments
            .Where(p =>
                (p.Date.Year == today.Year && p.Date.Month == today.Month) ||
                (p.Date.Year == nextMonth.Year && p.Date.Month == nextMonth.Month))
            .Sum(p => p.Amount);
    }

    public decimal RemainingMonthTotal()
        => RemainingMonthDirectDebits() + ProRatedBudget();

    public decimal ProjectedBalance()
        => BankBalance
           - RemainingMonthDirectDebits()
           - ProRatedBudget()
           - UpcomingCosts.Where(u => u.Date >= DateTime.Today).Sum(u => u.Amount)
           + RemainingOneOffIncomingPayments()
           + UnpaidIncome()
           - TotalCreditCardBalance();

    public decimal FullMonthlyBudget()
        => BudgetCategories.Sum(b => b.MonthlyAmount);

    public decimal NextMonthUpcomingCosts()
    {
        var nextMonth = DateTime.Today.AddMonths(1);
        return UpcomingCosts
            .Where(u => u.Date.Year == nextMonth.Year && u.Date.Month == nextMonth.Month)
            .Sum(u => u.Amount);
    }

    public decimal NextMonthDirectDebits()
    {
        var nextMonth = DateTime.Today.AddMonths(1);
        return DirectDebits
            .Where(d => d.DayOfMonth >= 1 && d.DayOfMonth <= DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month))
            .Sum(d => d.Amount);
    }

    public decimal ProjectedBalanceMinusNextMonthBills()
        => ProjectedBalance()
           - TotalMonthlyDirectDebits()
           - FullMonthlyBudget()
           - NextMonthUpcomingCosts()
           - TotalCreditCardBalance()
           + UnallocatedSavings();
    public decimal UnallocatedSavings()
        => SavingsPots.Sum(p => p.Amount) - SavingsSubPots.Sum(p => p.Amount);

    public decimal ProjectedBalanceWithSavings()
        => ProjectedBalance() + SavingsPots.Sum(s => s.Amount);

    public decimal ProjectedBalanceWithoutSavings()
        => ProjectedBalance();

    public decimal NextMonthForecastAfterBills()
        => BankBalance
           - RemainingMonthDirectDebits()
           - ProRatedBudget()
           - UpcomingCosts.Where(u => u.Date >= DateTime.Today).Sum(u => u.Amount)
           - NextMonthDirectDebits()
           - FullMonthlyBudget()
           - NextMonthUpcomingCosts()
           - TotalCreditCardBalance()
           + CurrentAndNextMonthOneOffIncomingPayments()
           + UnpaidIncome()
           + UnallocatedSavings();

    public List<(string Label, decimal Balance)> GetFutureForecast(int months)
    {
        if (months <= 0) return [];

        var result = new List<(string Label, decimal Balance)>(months);
        var totalMonthlyIncome = Incomes.Sum(i => i.Amount);

        // Month 1 uses the same logic as the "Forecast after next month's bills" card
        decimal balance = NextMonthForecastAfterBills();
        result.Add((DateTime.Today.AddMonths(1).ToString("MMM yyyy"), balance));

        // Subsequent months: rolling projection (recurring debits, budget, income; one-off upcoming costs per month)
        for (int m = 2; m <= months; m++)
        {
            var refDate = DateTime.Today.AddMonths(m);
            var upcomingForMonth = UpcomingCosts
                .Where(u => u.Date.Year == refDate.Year && u.Date.Month == refDate.Month)
                .Sum(u => u.Amount);
            var oneOffIncomingForMonth = OneOffPayments
                .Where(p => p.Date.Year == refDate.Year && p.Date.Month == refDate.Month)
                .Sum(p => p.Amount);

            balance = balance
                - TotalMonthlyDirectDebits()
                - FullMonthlyBudget()
                - upcomingForMonth
                + oneOffIncomingForMonth
                + totalMonthlyIncome;

            result.Add((refDate.ToString("MMM yyyy"), balance));
        }

        return result;
    }


    public async Task AddIncomeAsync(Income item)
    {
        Incomes.Add(item);
        await SaveIncomesAsync();
    }

    public async Task UpdateIncomeAsync(Income item)
    {
        var idx = Incomes.FindIndex(i => i.Id == item.Id);
        if (idx >= 0) Incomes[idx] = item;
        await SaveIncomesAsync();
    }

    public async Task RemoveIncomeAsync(Guid id)
    {
        Incomes.RemoveAll(i => i.Id == id);
        await SaveIncomesAsync();
    }

    public async Task ToggleIncomePaidAsync(Guid id, bool paid)
    {
        var idx = Incomes.FindIndex(i => i.Id == id);
        if (idx >= 0) Incomes[idx].PaidThisMonth = paid;
        await SaveIncomesAsync();
    }

    private async Task SaveDirectDebitsAsync()
        => await SaveToStorage("finance_directdebits", DirectDebits);

    private async Task SaveBudgetCategoriesAsync()
        => await SaveToStorage("finance_budgetcategories", BudgetCategories);

    private async Task SaveUpcomingCostsAsync()
        => await SaveToStorage("finance_upcomingcosts", UpcomingCosts);

    private async Task SaveOneOffPaymentsAsync()
        => await SaveToStorage("finance_oneoffpayments", OneOffPayments);

    private async Task SaveIncomesAsync()
        => await SaveToStorage("finance_incomes", Incomes);

    private async Task SaveToStorage<T>(string key, T value)
        => await _firestore.SaveAsync(key, value);

    private async Task<T?> LoadFromStorage<T>(string key)
        => await _firestore.LoadAsync<T>(key);
}
