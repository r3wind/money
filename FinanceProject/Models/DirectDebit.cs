namespace FinanceProject.Models;

public class DirectDebit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public string Details { get; set; } = string.Empty;
}
