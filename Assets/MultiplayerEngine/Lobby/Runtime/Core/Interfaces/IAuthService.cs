using System.Threading.Tasks;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manager-level interface for authentication operations.
    /// UI scripts resolve this via ServiceLocator instead of using AuthenticationManager.Instance.
    /// </summary>
    public interface IAuthService
    {
        Task RetryAuthAsync();
        Task OnManualSignInSuccess();
        Task OnManualSignUpSuccess();
        Task OnProfileSetupSuccess();
        Task<bool> SignInAsync(string email, string password, bool rememberMe);
        Task<bool> SignUpAsync(string email, string password, bool rememberMe);
        Task<bool> SignOutAsync();
    }
}
