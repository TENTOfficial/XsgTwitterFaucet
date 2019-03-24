using System.Linq;
using System.Threading.Tasks;
using XsgTwitterBot.Node;

namespace XsgTwitterBot.Services.Impl
{
    public class MessageParser : IMessageParser
    {
        private readonly INodeApi _nodeApi;

        public MessageParser(INodeApi nodeApi)
        {
            _nodeApi = nodeApi;
        }
        public async Task<string> GetValidAddressAsync(string text)
        {
            var addresses = text.Split(' ').Where(w => (w.StartsWith("s1") || w.StartsWith("s3")) && w.Length > 30)
                .Select(w => w.Trim());

            foreach (var address in addresses)
            {
                var isValid = (await _nodeApi.ValidateAddressAsync(address)).Result.IsValid;
                if (isValid)
                    return address;
            }

            return null;
        }
    }
}