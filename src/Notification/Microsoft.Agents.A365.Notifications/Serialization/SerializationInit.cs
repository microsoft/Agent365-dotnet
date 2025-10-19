using Microsoft.Agents.A365.Notifications.Serialization.Converters;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Notifications.Serialization
{
    [SerializationInit]
    internal class SerializationInit
    {
        public static void Init()
        {
            var converters = new List<JsonConverter>
            {
                new WpxCommentConverter(),
                new EmailReferenceConverter(),
            };
            ProtocolJsonSerializer.ApplyExtensionConverters(converters);
        }
    }
}
