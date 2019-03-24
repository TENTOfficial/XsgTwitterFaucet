using System.Threading.Tasks;

namespace XsgTwitterBot.Services
{
    public interface IMessageParser
    {
        Task<string> GetValidAddressAsync(string text);
    }
}