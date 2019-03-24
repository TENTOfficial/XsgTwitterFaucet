using Newtonsoft.Json;

namespace XsgTwitterBot.Node
{
    [JsonArray]
    public class AddressGrouping
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }
    }

   
}