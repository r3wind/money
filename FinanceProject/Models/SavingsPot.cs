using System.ComponentModel.DataAnnotations;

namespace FinanceProject.Models
{
    public class SavingsPot
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }

        [Range(0, 100, ErrorMessage = "AER must be between 0 and 100")]
        public decimal Aer { get; set; }
    }
}
