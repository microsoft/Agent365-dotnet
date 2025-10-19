using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Serialization.Converters;
using Microsoft.Agents.A365.Notifications.Models;
using System.Text.Json;


namespace Microsoft.Agents.A365.Notifications.Serialization.Converters
{
    internal class EmailReferenceConverter : ConnectorConverter<EmailReference>
    {
        protected override void ReadExtensionData(ref Utf8JsonReader reader, EmailReference value, string propertyName, JsonSerializerOptions options)
        {
            value.Properties.Add(propertyName, JsonSerializer.Deserialize<JsonElement>(ref reader, options));
        }

        protected override bool TryReadExtensionData(ref Utf8JsonReader reader, EmailReference value, string propertyName, JsonSerializerOptions options)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                var propertyValue = JsonSerializer.Deserialize<object>(ref reader, options);

                if (propertyValue == null) return false;

                foreach (var element in propertyValue.ToJsonElements())
                {
                    value.Properties.Add(element.Key, element.Value);
                }

                return true;
            }

            return false;
        }

        protected override bool TryWriteExtensionData(Utf8JsonWriter writer, EmailReference value, string propertyName)
        {
            if (propertyName.Equals(nameof(value.Properties)))
            {
                foreach (var extensionData in value.Properties)
                {
                    writer.WritePropertyName(extensionData.Key);
                    extensionData.Value.WriteTo(writer);
                }

                return true;
            }

            return false;
        }
    }
}
