using System.ComponentModel.DataAnnotations;

namespace FinanceProject.Models;

public class CreditCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Limit must be greater than zero")]
    public decimal Limit { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Available credit must be zero or more")]
    public decimal AvailableCredit { get; set; }

    public decimal Balance => Limit - AvailableCredit;
}
