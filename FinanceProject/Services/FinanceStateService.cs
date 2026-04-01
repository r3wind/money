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

    public decimal BankBalance { get; private set; }

    public FinanceStateService(FirestoreService firestore)
    {
        _firestore = firestore;
    }

    // ---------------------------------------------------------
    // LOAD / SAVE
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // DIRECT DEBITS
    // ---------------------------------------------------------
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

    private Task SaveDirectDebitsAsync()
        => SaveToStorage("finance_directdebits", DirectDebits);

    // ---------------------------------------------------------
    // BUDGET CATEGORIES
    // ---------------------------------------------------------
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

    private Task SaveBudgetCategoriesAsync()
        => SaveToStorage("finance_budgetcategories", BudgetCategories);

    // ---------------------------------------------------------
    // UPCOMING COSTS
    // ---------------------------------------------------------
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

    private Task SaveUpcomingCostsAsync()
        => SaveToStorage("finance_upcomingcosts", UpcomingCosts);

    // ---------------------------------------------------------
    // ONE-OFF PAYMENTS
    // ---------------------------------------------------------
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

    private Task SaveOneOffPaymentsAsync()
        => SaveToStorage("finance_oneoffpayments", OneOffPayments);

    // ---------------------------------------------------------
    // INCOME
    // ---------------------------------------------------------
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

    private Task SaveIncomesAsync()
        => SaveToStorage("finance_incomes", Incomes);

    // ---------------------------------------------------------
    // SAVINGS POTS
    // ---------------------------------------------------------
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

    private Task SaveSavingsPotsAsync()
        => SaveToStorage("finance_savingspots", SavingsPots);

    // ---------------------------------------------------------
    // SAVINGS SUB-POTS
    // ---------------------------------------------------------
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

    private Task SaveSavingsSubPotsAsync()
        => SaveToStorage("finance_savingssubpots", SavingsSubPots);

    // ---------------------------------------------------------
    // CREDIT CARDS
    // ---------------------------------------------------------
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

    private Task SaveCreditCardsAsync()
        => SaveToStorage("finance_creditcards", CreditCards);

    // ---------------------------------------------------------
    // STORAGE HELPERS
    // ---------------------------------------------------------
    private async Task SaveToStorage<T>(string key, T value)
        => await _firestore.SaveAsync(key, value);

    private async Task<T?> LoadFromStorage<T>(string key)
        => await _firestore.LoadAsync<T>(key);
}
