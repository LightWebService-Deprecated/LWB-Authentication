using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LWS_Authentication;
using LWS_Authentication.Configuration;
using LWS_Authentication.Model;
using LWS_Authentication.Repository;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace LWS_AuthenticationTest.Repository
{
    public class AccountRepositoryTest: IDisposable
    {
        private readonly IMongoCollection<Account> _accountCollection;
        private readonly IAccountRepository _accountRepository;

        private readonly MongoContext _mongoContext;
        private readonly MongoConfiguration _mongoConfiguration;

        private Account MockAccount => new Account
        {
            UserEmail = Guid.NewGuid().ToString(),
            UserPassword = Guid.NewGuid().ToString(),
            UserAccessTokens = new List<AccessToken>()
        };

        private RegisterRequestMessage GetRegisterMessageFromUser(Account account)
        {
            return new RegisterRequestMessage
            {
                UserEmail = account.UserEmail,
                UserPassword = account.UserPassword
            };
        }

        private LoginRequestMessage GetLoginMessageFromUser(Account account = null)
        {
            return new LoginRequestMessage
            {
                UserEmail = (account == null) ? Guid.NewGuid().ToString() : account.UserEmail,
                UserPassword = (account == null) ? Guid.NewGuid().ToString() : account.UserPassword
            };
        }

        public AccountRepositoryTest()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("integration_test.json")
                .Build();
            
            // Create Mongo Config
            _mongoConfiguration = configuration.GetSection("MongoConfiguration").Get<MongoConfiguration>();
            _mongoConfiguration.MongoDbName = "AccountRepositoryTest"; // Override
            
            // Create MongoContext
            _mongoContext = new MongoContext(_mongoConfiguration);

            _accountCollection = _mongoContext.MongoDatabase.GetCollection<Account>(nameof(Account));
            _accountCollection.Indexes.CreateOne(
                new CreateIndexModel<Account>(
                    new BsonDocument {{"userEmail", 1}},
                    new CreateIndexOptions {Unique = true}));
            _accountRepository = new AccountRepository(_mongoContext);
        }

        public void Dispose()
        {
            _mongoContext.MongoClient.DropDatabase(_mongoConfiguration.MongoDbName);
        }

        private async Task<List<Account>> GetAllAccountListAsync()
        {
            var findOption = Builders<Account>.Filter.Empty;
            return await (await _accountCollection.FindAsync(findOption)).ToListAsync();
        }

        [Fact(DisplayName =
            "CreateAccountAsync: CreateAccountAsync should throw an exception if duplicated error occurred.")]
        public async void Is_CreateAccountAsync_Throws_Exception_Error()
        {
            // Let
            var mockAccount = MockAccount;
            var registerMessage = GetRegisterMessageFromUser(mockAccount);
            await _accountCollection.InsertOneAsync(mockAccount);
            
            // Do
            await Assert.ThrowsAsync<MongoWriteException>(() => _accountRepository.CreateAccountAsync(registerMessage));
            
            // Check
            Assert.Single(await GetAllAccountListAsync());
        }

        [Fact(DisplayName = "CreateAccountAsync: CreateAccountAsync should work ok when completes.")]
        public async void Is_CreateAccountAsync_Works_Well()
        {
            // Let
            var registerMessage = GetRegisterMessageFromUser(MockAccount);
            
            // Do
            await _accountRepository.CreateAccountAsync(registerMessage);
            
            // Check
            var dbList = await GetAllAccountListAsync();
            Assert.Single(dbList);
            Assert.Equal(registerMessage.UserEmail, dbList[0].UserEmail);
            Assert.Equal(registerMessage.UserPassword, dbList[0].UserPassword);
        }

        [Fact(DisplayName =
            "LoginAccountAsync: LoginAccountAsync should return null when entity is not found.")]
        public async void Is_LoginAccountAsync_Returns_Null_When_Authenticate_Fails()
        {
            // Let
            var loginRequest = GetLoginMessageFromUser();
            
            // Do
            var result = await _accountRepository.LoginAccountAsync(loginRequest);
            
            // Check
            Assert.Null(result);
            Assert.Empty(await GetAllAccountListAsync());
        }

        [Fact(DisplayName = "LoginAccountAsync: LoginAccountAsync should return object when exists.")]
        public async void Is_LoginAccountAsync_Works_Well()
        {
            // Let
            var mockUser = MockAccount;
            await _accountCollection.InsertOneAsync(mockUser);
            var mockLoginRequest = GetLoginMessageFromUser(mockUser);
            
            // Do
            var result = await _accountRepository.LoginAccountAsync(mockLoginRequest);
            
            // Check
            Assert.NotNull(result);
            Assert.Equal(mockUser.UserEmail, result.UserEmail);
            Assert.Equal(mockUser.UserPassword, result.UserPassword);
        }

        [Fact(DisplayName = "SaveAccessTokenAsync: SaveAccessTokenAsync should save its access token well")]
        public async void Is_SaveAccessTokenAsync_Works_Well()
        {
            // Let
            var mockUser = MockAccount;
            await _accountCollection.InsertOneAsync(mockUser);
            
            // Do
            var accessToken = new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(20).ToUnixTimeSeconds(),
                Token = "testToken"
            };
            await _accountRepository.SaveAccessTokenAsync(mockUser.UserEmail, accessToken);
            
            // Check
            var accountList = await GetAllAccountListAsync();
            Assert.Single(accountList);
            Assert.Single(accountList[0].UserAccessTokens);
            Assert.Equal(accessToken.CreatedAt, accountList[0].UserAccessTokens[0].CreatedAt);
            Assert.Equal(accessToken.ExpiresAt, accountList[0].UserAccessTokens[0].ExpiresAt);
            Assert.Equal(accessToken.Token, accountList[0].UserAccessTokens[0].Token);
        }

        [Fact(DisplayName =
            "AuthenticateUserAsync: AuthenticateUserAsync should return account token when succeed to find user.")]
        public async void Is_AuthenticateUserAsync_Returns_AccessToken_When_Exists()
        {
            // Let
            var mockUser = MockAccount;
            var accessToken = new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(20).ToUnixTimeSeconds(),
                Token = "TEst"
            };
            mockUser.UserAccessTokens.Add(accessToken);
            await _accountCollection.InsertOneAsync(mockUser);
            
            // Do
            var result = await _accountRepository.AuthenticateUserAsync(accessToken.Token);
            
            // Check
            Assert.NotNull(result);
            Assert.Equal(mockUser.UserEmail, result.UserEmail);
        }

        [Fact(DisplayName =
            "AuthenticateUserAsync: AuthenticateUserAsync should return null when failed to authenticate.")]
        public async void Is_AuthenticateUserAsync_Returns_Null_When_Failed_To_Authenticate()
        {
            var result = await _accountRepository.AuthenticateUserAsync("accessToken.Token");
            Assert.Null(result);
        }

        [Fact(DisplayName = "AuthenticateUserAsync: AuthenticateUserAsync should return null when token is expired")]
        public async void Is_AuthenticateUserAsync_Returns_Null_When_Token_Is_Expired()
        {
            // Let
            var mockUser = MockAccount;
            var accessToken = new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.MinValue.ToUnixTimeSeconds(),
                Token = "TEst"
            };
            mockUser.UserAccessTokens.Add(accessToken);
            await _accountCollection.InsertOneAsync(mockUser);
            
            // Do
            var result = await _accountRepository.AuthenticateUserAsync(accessToken.Token);
            
            // Check
            Assert.Null(result);
        }

        [Fact(DisplayName = "DropoutUserAsync: DropoutUserAsync should remove user well.")]
        public async void Is_DropoutUserAsync_Removes_User_Well()
        {
            // Let
            var mockUser = MockAccount;
            var accessToken = new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.MinValue.ToUnixTimeSeconds(),
                Token = "TEst"
            };
            mockUser.UserAccessTokens.Add(accessToken);
            
            // Do
            await _accountRepository.DropoutUserAsync(mockUser.UserEmail);
            
            // Check
            var list = await GetAllAccountListAsync();
            Assert.Empty(list);
        }
    }
}