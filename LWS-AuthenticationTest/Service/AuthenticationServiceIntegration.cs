using System;
using Grpc.Net.Client;
using LWS_Authentication;
using LWS_Authentication.Configuration;
using LWS_Authentication.Model;
using LWS_Authentication.Repository;
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
            var targetAccount = new Account {UserEmail = Guid.NewGuid().ToString(), UserPassword = Guid.NewGuid().ToString()};
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
    }
}