namespace FinanceProject.Models;

public class OneOffPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Details { get; set; } = string.Empty;
}
