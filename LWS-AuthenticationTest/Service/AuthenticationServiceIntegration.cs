using System;
using System.Collections.Generic;
using Grpc.Net.Client;
using LWS_Authentication;
using LWS_Authentication.Configuration;
using LWS_Authentication.Model;
using LWS_Authentication.Repository;
using LWS_Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Xunit;

namespace LWS_AuthenticationTest.Service
{
    // Not the GRPC Calling test, but it does integrates all of dependencies to real one.
    public class AuthenticationServiceIntegration: IDisposable
    {
        private readonly IMongoCollection<Account> _accountCollection;
        private readonly AuthenticationService _authenticationService;
        private readonly MongoConfiguration _mongoConfiguration;
        private readonly MongoContext _mongoContext;
        
        public AuthenticationServiceIntegration()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("integration_test.json")
                .Build();
            
            // Create Mongo Config
            _mongoConfiguration= configuration.GetSection("MongoConfiguration").Get<MongoConfiguration>();
            _mongoConfiguration.MongoDbName = "AuthenticationServiceIntegration"; // Override
            
            // Create MongoContext
            _mongoContext = new MongoContext(_mongoConfiguration);
            _accountCollection = _mongoContext.MongoDatabase.GetCollection<Account>(nameof(Account));

            // Create object
            _authenticationService = new AuthenticationService(NullLogger<AuthenticationService>.Instance, new AccountRepository(_mongoContext));
        }
        
        private Account MockAccount => new Account
        {
            UserEmail = Guid.NewGuid().ToString(),
            UserPassword = Guid.NewGuid().ToString(),
            UserAccessTokens = new List<AccessToken>()
        };

        public void Dispose()
        {
            _mongoContext.MongoClient.DropDatabase(_mongoConfiguration.MongoDbName);
        }

        [Fact(DisplayName =
            "RegisterRequest: RegisterRequest should return result code DUPLICATE when duplicated user tried to register.")]
        public async void Is_RegisterRequest_Returns_Duplicate_When_Duplicated_User()
        {
            // Let
            var targetAccount = new Account {UserEmail = Guid.NewGuid().ToString()};
            await _accountCollection.InsertOneAsync(targetAccount);
            
            // Do
            var message = new RegisterRequestMessage
            {
                UserEmail = targetAccount.UserEmail,
                UserPassword = "test"
            };
            var result = await _authenticationService.RegisterRequest(message, null);
            
            // Check
            Assert.Equal(ResultCode.Duplicate, result.ResultCode);
        }
        

        [Fact(DisplayName =
            "RegisterRequest: RegisterRequest should return result code SUCCESS when registering user completes.")]
        public async void Is_RegisterRequest_Returns_Succeeds()
        {
            // Let
            var message = new RegisterRequestMessage
            {
                UserEmail = Guid.NewGuid().ToString(),
                UserPassword = "testPassword"
            };
            
            // Do
            var result = await _authenticationService.RegisterRequest(message, null);
            
            // Check
            Assert.Equal(ResultCode.Success, result.ResultCode);
        }

        [Fact(DisplayName =
            "LoginRequest: LoginRequest should return Forbidden when either email or password is wrong.")]
        public async void Is_LoginRequest_Returns_Forbidden_When_Credential_Wrong()
        {
            // Let
            var message = new LoginRequestMessage
            {
                UserEmail = Guid.NewGuid().ToString(),
                UserPassword = Guid.NewGuid().ToString()
            };
            
            // Do
            var result = await _authenticationService.LoginRequest(message, null);
            
            // Check
            Assert.Equal(ResultCode.Forbidden, result.ResultCode);
        }

        [Fact(DisplayName = "LoginRequest: LoginRequest should return Succeed and its token when login completes.")]
        public async void Is_LoginRequest_Works_Well()
        {
            // Let
            var targetAccount = new Account {UserEmail = Guid.NewGuid().ToString(), UserPassword = Guid.NewGuid().ToString(), UserAccessTokens = new List<AccessToken>()};
            await _accountCollection.InsertOneAsync(targetAccount);
            
            // Do
            var message = new LoginRequestMessage
            {
                UserEmail = targetAccount.UserEmail,
                UserPassword = targetAccount.UserPassword
            };
            var result = await _authenticationService.LoginRequest(message, null);

            // Check
            Assert.Equal(ResultCode.Success, result.ResultCode);
            var objectReturned = JsonConvert.DeserializeObject<AccessToken>(result.Content);
            Assert.NotNull(objectReturned);
        }

        [Fact(DisplayName =
            "AuthenticateUserRequest: AuthenticateUserRequest should return succeeds and account info when authenticating succeeds.")]
        public async void Is_AuthenticateUserRequest_Returns_Succeeds()
        {
            // Let
            var mockUser = MockAccount;
            var accessToken = new AccessToken
            {
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds(),
                Token = "TEst"
            };
            mockUser.UserAccessTokens.Add(accessToken);
            await _accountCollection.InsertOneAsync(mockUser);
            
            // Do
            var result =
                await _authenticationService.AuthenticateUserRequest(
                    new AuthenticateUserMessage {UserToken = accessToken.Token}, null);
            
            // Check
            Assert.Equal(ResultCode.Success, result.ResultCode);
            var objectJson = JsonConvert.DeserializeObject<AccountProjection>(result.Content);
            Assert.NotNull(objectJson);
            Assert.Equal(objectJson.UserEmail, mockUser.UserEmail);
        }

        [Fact(DisplayName =
            "AuthenticateUserRequest: AuthenticateUserRequest should return forbidden when either token is expired or not exists.")]
        public async void Is_AuthenticateUserRequest_Returns_Null_When_Token_Expired()
        {
            var result =
                await _authenticationService.AuthenticateUserRequest(
                    new AuthenticateUserMessage {UserToken = "accessToken.Token"}, null);
            
            Assert.Equal(ResultCode.Forbidden, result.ResultCode);
        }
    }
}