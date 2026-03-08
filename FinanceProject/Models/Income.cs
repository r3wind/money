namespace FinanceProject.Models;

public class Income
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool PaidThisMonth { get; set; } = false;
    public string Details { get; set; } = string.Empty;
}
