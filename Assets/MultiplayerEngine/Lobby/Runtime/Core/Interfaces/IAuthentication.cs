using System.Threading.Tasks;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for user authentication and profile management.
    /// </summary>
    public interface IAuthentication
    {
        Task InitializeAsync();
        /// <summary>
        /// Attempts to automatically sign in the user if a previous session exists.
        /// </summary>
        /// <returns>True if sign-in was successful; otherwise, false.</returns>
        Task<bool> AutoSignInAsync();

        /// <summary>
        /// Registers a new user with the specified credentials.
        /// </summary>
        /// <param name="email">User's email address.</param>
        /// <param name="password">User's password.</param>
        /// <param name="rememberMe">Whether to persist the session.</param>
        /// <returns>True if sign-up was successful; otherwise, false.</returns>
        Task<bool> SignUpAsync(string email, string password, bool rememberMe);

        /// <summary>
        /// Signs in a user with the specified credentials.
        /// </summary>
        /// <param name="email">User's email address.</param>
        /// <param name="password">User's password.</param>
        /// <param name="rememberMe">Whether to persist the session.</param>
        /// <returns>True if sign-in was successful; otherwise, false.</returns>
        Task<bool> SignInAsync(string email, string password, bool rememberMe);

        /// <summary>
        /// Signs in a user using an authentication code.
        /// </summary>
        /// <param name="authCode">Authentication code.</param>
        /// <returns>True if sign-in was successful; otherwise, false.</returns>
        Task<bool> SignInWithCodeAsync(string authCode);

        /// <summary>
        /// Signs out the current user.
        /// </summary>
        /// <returns>True if sign-out was successful; otherwise, false.</returns>
        Task<bool> SignOutAsync();

        /// <summary>
        /// Starts automatic sign-in if a previous session exists.
        /// Invokes the callback with true if sign-in was successful; otherwise, false.
        /// </summary>
        /// <remarks>
        /// This is a Unity main thread-safe callback wrapper for AutoSignInAsync.
        /// </remarks>
        async void AutoSignIn(System.Action<bool> callback)
        {
            var result = await AutoSignInAsync();
            callback?.Invoke(result);
        }

        /// <summary>
        /// Registers a new user and invokes the callback with the result.
        /// </summary>
        async void SignUp(string email, string password, bool rememberMe, System.Action<bool> callback)
        {
            var result = await SignUpAsync(email, password, rememberMe);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Initiates sign-in and invokes the callback with the result.
        /// </summary>
        async void SignIn(string email, string password, bool rememberMe, System.Action<bool> callback)
        {
            var result = await SignInAsync(email, password, rememberMe);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Signs in with an authentication code and invokes the callback with the result.
        /// </summary>
        async void SignInWithCode(string authCode, System.Action<bool> callback)
        {
            var result = await SignInWithCodeAsync(authCode);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Signs out the current user and invokes the callback with the result.
        /// </summary>
        async void SignOut(System.Action<bool> callback)
        {
            var result = await SignOutAsync();
            callback?.Invoke(result);
        }
    }
}
