using System.ComponentModel.DataAnnotations;

namespace Derivative.Models
{
  public class BalanceInputModel : BarInputModel
  {
    [Required]
    public double Price { get; set; }
  }
}
