using System.Threading.Tasks;

namespace XsgTwitterBot.Node
{
    public interface INodeApi
    {
        Task<JsonRpcResponse<GetInfoResponse>> GetInfoAsync();

        Task<string> SendToAddressAsync(string address, decimal amount);

        Task<JsonRpcResponse<ValidateAddressResponse>> ValidateAddressAsync(string address);
    }
}
