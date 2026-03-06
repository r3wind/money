namespace FinanceProject.Models;

public class BudgetCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyAmount { get; set; }
}
