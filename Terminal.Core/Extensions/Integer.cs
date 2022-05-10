namespace Terminal.Core.ExtensionSpace
{
  public static class IntegerExtensions
  {
    public static int ToInt(this int? input, int value = 0)
    {
      if (input is null)
      {
        return value;
      }

      return input.Value;
    }
  }
}
