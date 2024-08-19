using System.ComponentModel.DataAnnotations;

namespace Derivative.Models
{
  public class MapInputModel : BaseOptionInputModel
  {
    [Required]
    public string Expression { get; set; }
  }
}
