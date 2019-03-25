using System.Threading.Tasks;

namespace XsgTwitterBot.Services
{
    public interface ISyncCheckService
    {
        Task WaitUntilSyncedAsync();
    }
}