using System.Threading.Tasks;

namespace XsgTwitterBot.Services
{
    public interface IExplorerApi
    {
        Task<long> GetLastBlock();
    }
}