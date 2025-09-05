using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Core.Common.Services
{
  public class ConversionService
  {
    /// <summary>
    /// Serialization options
    /// </summary>
    public virtual JsonSerializerOptions Options { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ConversionService()
    {
      Options = new JsonSerializerOptions
      {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters =
        {
          new Converters.CustomConverter<bool>(),
          new Converters.CustomConverter<byte>(),
          new Converters.CustomConverter<sbyte>(),
          new Converters.CustomConverter<short>(),
          new Converters.CustomConverter<ushort>(),
          new Converters.CustomConverter<int>(),
          new Converters.CustomConverter<uint>(),
          new Converters.CustomConverter<long>(),
          new Converters.CustomConverter<ulong>(),
          new Converters.CustomConverter<float>(),
          new Converters.CustomConverter<double>(),
          new Converters.CustomConverter<decimal>(),
          new Converters.CustomConverter<char>(),
          new Converters.CustomConverter<string>(),
          new Converters.CustomConverter<DateOnly>(),
          new Converters.CustomConverter<TimeOnly>(),
          new Converters.CustomConverter<DateTime>()
        },
        TypeInfoResolver = GetResolver()
      };
    }

    /// <summary>
    /// Create more resolver with more permissive naming policy for better matching
    /// </summary>
    public virtual DefaultJsonTypeInfoResolver GetResolver() => new()
    {
      Modifiers =
      {
        contract => contract.Properties.ToList().ForEach(property =>
        {
          var name = Regex.Replace(property.Name,"(.)([A-Z])","$1_$2");

          if (string.Equals(name, property.Name))
          {
            return;
          }

          var o = contract.CreateJsonPropertyInfo(property.PropertyType, name);

          o.Set = property.Set;
          o.AttributeProvider = property.AttributeProvider;

          contract.Properties.Add(o);
        })
      }
    };

    /// <summary>
    /// Get compound key for grains
    /// </summary>
    public virtual string Compose(object descriptor) => JsonSerializer.Serialize(descriptor, Options);

    /// <summary>
    /// Read compound key for grains
    /// </summary>
    public virtual T Decompose<T>(string descriptor) => JsonSerializer.Deserialize<T>(descriptor, Options);
  }
}
