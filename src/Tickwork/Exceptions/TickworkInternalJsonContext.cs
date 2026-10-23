using System.Text.Json.Serialization;

namespace Tickwork.Exceptions
{
    [JsonSerializable(typeof(ExceptionDetailClassForSerialization))]
    internal partial class TickworkInternalJsonContext : JsonSerializerContext { }
}
