using System.Threading.Tasks;

namespace XsgTwitterBot.Services
{
    public interface IWithdrawalService
    {
        Task<bool> CanExecuteAsync();
        Task ExecuteAsync(string targetAddress);
    }
}