using Newtonsoft.Json;

namespace XsgTwitterBot.Node
{
    public class ValidateAddressResponse
    {
        [JsonProperty("isvalid")]
        public bool IsValid { get; set; }
    }
}