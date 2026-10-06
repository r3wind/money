using System.ComponentModel.DataAnnotations;

namespace FinanceProject.Models
{
    public class SavingsSubPot
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;
        [Range(0, double.MaxValue, ErrorMessage = "Amount must not be negative")]
        public decimal Amount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Monthly contribution must not be negative")]
        public decimal MonthlyContribution { get; set; }

        public DateOnly? LastContributionMonth { get; set; }

        public bool ApplyMonthlyContributions(DateOnly today)
        {
            if (MonthlyContribution <= 0)
                return false;

            var currentMonth = new DateOnly(today.Year, today.Month, 1);
            if (LastContributionMonth is not { } lastMonth)
            {
                LastContributionMonth = currentMonth;
                return true;
            }

            var monthsDue = (currentMonth.Year - lastMonth.Year) * 12
                + currentMonth.Month - lastMonth.Month;
            if (monthsDue <= 0)
                return false;

            Amount += MonthlyContribution * monthsDue;
            LastContributionMonth = currentMonth;
            return true;
        }
    }
}
