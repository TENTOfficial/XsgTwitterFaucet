using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XsgTwitterBot.Services.Impl
{
    public class ExplorerApi : IExplorerApi
    {
        static readonly HttpClient HttpClient = new HttpClient();

        public async Task<long> GetLastBlock()
        {
            var response = await HttpClient.GetAsync("https://explorer.snowgem.org/api/blocks");
            var content = await response.Content.ReadAsStringAsync();

            JObject result = JsonConvert.DeserializeObject<dynamic>(content);
            var height = result["blocks"].Children().First().Children().Children().First().Value<long>();
            return height;
        }
    }
}
