using MudBlazor;
using System.ComponentModel.DataAnnotations;

namespace Derivative.Models
{
  public class BaseOptionInputModel
  {
    public string Group { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public DateRange Range { get; set; }
  }
}
