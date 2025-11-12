using System.ComponentModel;
using System.Text.Json.Serialization;

namespace BriAgent.Backend.Models
{
    [Description("Información sobre una persona incluyendo nombre, edad y ocupación")]
    public class PersonInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("occupation")]
        public string? Occupation { get; set; }
    }
}
