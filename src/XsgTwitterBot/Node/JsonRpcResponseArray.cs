using System.Collections.Generic;
using Newtonsoft.Json;

namespace XsgTwitterBot.Node
{
    public class JsonRpcResponseArray<TResult>
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("result")]
        public List<TResult> Result { get; set; }
    }
}