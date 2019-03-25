using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XsgTwitterBot.Configuration;

namespace XsgTwitterBot.Node.Impl
{
    public class NodeApi : INodeApi
    {
        private readonly NodeOptions _options;

        public NodeApi(NodeOptions options)
        {
            _options = options;
        }

        public async Task<string> ExecuteJsonRpcCommandAsync(string method, Dictionary<string, object> @params)
        {
            var request = (HttpWebRequest)WebRequest.Create(_options.Url);
            request.Credentials = new NetworkCredential(_options.AuthUserName, _options.AuthUserPassword);
            request.ContentType = "application/json-rpc";
            request.Method = "POST";

            var json = new JObject
            {
                new JProperty("jsonrpc", "1.0"), new JProperty("id", "1"), new JProperty("method", method)
            };

            if (@params.Keys.Count == 0)
            {
                json.Add(new JProperty("params", new JArray()));
            }
            else
            {
                json.Add(new JProperty("params", new JArray(@params.Reverse().Select(x => x.Value).ToList())));

            }

            var log = JsonConvert.SerializeObject(json);
            
            using (var dataStream = request.GetRequestStream())
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json));
                dataStream.Write(byteArray, 0, byteArray.Length);
                request.ContentLength = byteArray.Length;
            }
                
            using (var response = await request.GetResponseAsync())
            using (var ms = new MemoryStream())
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream != null) await responseStream.CopyToAsync(ms);
                ms.Position = 0;
                using (var reader = new StreamReader(ms))
                {
                   return reader.ReadToEnd();
                }
            }
        }

        public async Task<JsonRpcResponse<GetInfoResponse>> GetInfoAsync()
        {
            var result = await ExecuteJsonRpcCommandAsync("getinfo", new Dictionary<string, object>());
            return JsonConvert.DeserializeObject<JsonRpcResponse<GetInfoResponse>>(result);
        }

        public async Task<string> SendToAddressAsync(string address, decimal amount)
        {
            var txid = await ExecuteJsonRpcCommandAsync("sendtoaddress", new Dictionary<string, object>
            {
                ["amount"] = amount,
                ["address"] = address
            });

            return txid;
        }
        
        public async Task<JsonRpcResponse<ValidateAddressResponse>> ValidateAddressAsync(string address)
        {
            var result = await ExecuteJsonRpcCommandAsync("validateaddress", new Dictionary<string, object>
            {
                ["address"] = address,
            });

            return JsonConvert.DeserializeObject<JsonRpcResponse<ValidateAddressResponse>>(result);
        }
    }
}