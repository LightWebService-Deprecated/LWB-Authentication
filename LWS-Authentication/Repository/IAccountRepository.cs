using System.Threading.Tasks;
using LWS_Authentication.Model;

namespace LWS_Authentication.Repository
{
    public interface IAccountRepository
    {
        /// <summary>
        /// Create Account Asynchronously. If duplicated accounts are found, it will throw exceptions.
        /// </summary>
        /// <param name="message">Request Message from client.</param>
        /// <returns>None</returns>
        public Task CreateAccountAsync(RegisterRequestMessage message);

        /// <summary>
        /// Try authenticating user with received email/password.
        /// </summary>
        /// <param name="message">Login Request</param>
        /// <returns>Account Information when succeeds to authenticate, or null if failed.</returns>
        public Task<Account> LoginAccountAsync(LoginRequestMessage message);
        
        /// <summary>
        /// Save Pre-Created Access Token to Account.
        /// </summary>
        /// <param name="userEmail">User Email(Account Identifier)</param>
        /// <param name="accessToken">Access Token to Save.</param>
        /// <returns>Saved Access Token(which is identical to input accessToken)</returns>
        public Task<AccessToken> SaveAccessTokenAsync(string userEmail, AccessToken accessToken);
    }
}