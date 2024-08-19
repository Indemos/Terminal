using System.ComponentModel.DataAnnotations;

namespace Derivative.Models
{
  public class BarInputModel : BaseOptionInputModel
  {
    [Required]
    public string ExpressionUp { get; set; }

    [Required]
    public string ExpressionDown { get; set; }
  }
}
