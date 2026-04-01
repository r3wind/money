namespace FinanceProject.Services;

public class FinanceCalculator
{
    private readonly FinanceStateService _state;
    private readonly DateTime _today = DateTime.Today;

    public FinanceCalculator(FinanceStateService state)
    {
        _state = state;
    }

    // -----------------------------
    // BASIC TOTALS
    // -----------------------------
    public decimal TotalIncome => _state.Incomes.Sum(i => i.Amount);
    public decimal TotalDirectDebits => _state.DirectDebits.Sum(d => d.Amount);
    public decimal TotalBudget => _state.BudgetCategories.Sum(b => b.MonthlyAmount);

    public decimal Outgoings => TotalDirectDebits + TotalBudget;
    public decimal DisposableIncome => TotalIncome - Outgoings;

    // -----------------------------
    // PERCENTAGES
    // -----------------------------
    public int IncomePct =>
        TotalIncome > 0
            ? (int)Math.Round(100 * (TotalIncome - Outgoings) / TotalIncome)
            : 0;

    public int OutgoingsPct =>
        TotalIncome > 0
            ? (int)Math.Round(100 * Outgoings / TotalIncome)
            : 0;

    // -----------------------------
    // REMAINING MONTH CALCULATIONS
    // -----------------------------
    public decimal RemainingDirectDebits =>
        _state.DirectDebits
            .Where(d => d.DayOfMonth > _today.Day)
            .Sum(d => d.Amount);

    public decimal ProRatedBudget
    {
        get
        {
            var daysInMonth = DateTime.DaysInMonth(_today.Year, _today.Month);
            var remainingDays = daysInMonth - _today.Day + 1;
            var fraction = (decimal)remainingDays / daysInMonth;
            return TotalBudget * fraction;
        }
    }

    public decimal UpcomingCostsThisMonth =>
        _state.UpcomingCosts
            .Where(u =>
                u.Date > _today &&
                u.Date.Month == _today.Month &&
                u.Date.Year == _today.Year)
            .Sum(u => u.Amount);

    public decimal UpcomingCostsNextMonth =>
        _state.UpcomingCosts
            .Where(u =>
                u.Date.Month == _today.AddMonths(1).Month &&
                u.Date.Year == _today.AddMonths(1).Year)
            .Sum(u => u.Amount);

    // -----------------------------
    // INCOME & PAYMENTS
    // -----------------------------
    public decimal OneOffIncomingThisMonth =>
        _state.OneOffPayments
            .Where(p =>
                p.Date >= _today &&
                p.Date.Month == _today.Month &&
                p.Date.Year == _today.Year)
            .Sum(p => p.Amount);

    public decimal OneOffIncomingNextMonth =>
        _state.OneOffPayments
            .Where(p =>
                p.Date.Month == _today.AddMonths(1).Month &&
                p.Date.Year == _today.AddMonths(1).Year)
            .Sum(p => p.Amount);

    public decimal UnpaidIncome =>
        _state.Incomes.Where(i => !i.PaidThisMonth).Sum(i => i.Amount);

    // -----------------------------
    // SAVINGS
    // -----------------------------
    public decimal CreditCardBalance =>
        _state.CreditCards.Sum(c => c.Balance);

    public decimal UnallocatedSavings =>
        _state.SavingsPots.Sum(p => p.Amount) -
        _state.SavingsSubPots.Sum(p => p.Amount);

    public decimal AllocatedSavings =>
        _state.SavingsSubPots.Sum(p => p.Amount);

    // -----------------------------
    // END OF MONTH FORECASTS
    // -----------------------------
    public decimal EndOfMonthBalance =>
        _state.BankBalance
        - RemainingDirectDebits
        - ProRatedBudget
        - UpcomingCostsThisMonth
        + OneOffIncomingThisMonth
        + UnpaidIncome
        - CreditCardBalance;

    public decimal EndOfMonthBalanceExcIncome =>
        _state.BankBalance
        - RemainingDirectDebits
        - ProRatedBudget
        - UpcomingCostsThisMonth
        - CreditCardBalance;

    // -----------------------------
    // NEXT MONTH FORECAST
    // -----------------------------
    public decimal NextMonthForecast(bool includeNextMonthIncome)
    {
        var nextMonthIncome = includeNextMonthIncome ? TotalIncome : 0;

        return _state.BankBalance
            - RemainingDirectDebits
            - ProRatedBudget
            - UpcomingCostsThisMonth
            - TotalDirectDebits
            - TotalBudget
            - UpcomingCostsNextMonth
            - CreditCardBalance
            + OneOffIncomingThisMonth
            + OneOffIncomingNextMonth
            + UnpaidIncome
            + nextMonthIncome
            + UnallocatedSavings;
    }

    public List<(string Label, decimal Balance)> GetFutureForecast(int months)
    {
        if (months <= 0)
        {
            return [];
        }

        var result = new List<(string Label, decimal Balance)>(months);
        var balance = NextMonthForecast(false);
        result.Add((_today.AddMonths(1).ToString("MMM yyyy"), balance));

        for (var monthIndex = 2; monthIndex <= months; monthIndex++)
        {
            var targetMonth = _today.AddMonths(monthIndex);
            var upcomingCosts = _state.UpcomingCosts
                .Where(u => u.Date.Year == targetMonth.Year && u.Date.Month == targetMonth.Month)
                .Sum(u => u.Amount);
            var oneOffIncoming = _state.OneOffPayments
                .Where(p => p.Date.Year == targetMonth.Year && p.Date.Month == targetMonth.Month)
                .Sum(p => p.Amount);

            balance = balance
                - TotalDirectDebits
                - TotalBudget
                - upcomingCosts
                + oneOffIncoming
                + TotalIncome;

            result.Add((targetMonth.ToString("MMM yyyy"), balance));
        }

        return result;
    }
}
