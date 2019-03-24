using Newtonsoft.Json;

namespace XsgTwitterBot.Node
{
    public class JsonRpcResponse<TResult>
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("result")]
        public TResult Result { get; set; }
    }
}