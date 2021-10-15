using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LWS_Authentication.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LWS_Authentication.Repository
{
    public class AccountRepository: IAccountRepository
    {
        private readonly IMongoCollection<Account> _accountCollection;

        public AccountRepository(MongoContext mongoContext)
        {
            _accountCollection = mongoContext.MongoDatabase.GetCollection<Account>(nameof(Account));
            _accountCollection.Indexes.CreateOne(
                new CreateIndexModel<Account>(
                    new BsonDocument {{"userEmail", 1}},
                    new CreateIndexOptions {Unique = true}));
        }

        public async Task CreateAccountAsync(RegisterRequestMessage message)
        {
            await _accountCollection.InsertOneAsync(new Account
            {
                UserEmail = message.UserEmail,
                UserPassword = message.UserPassword,
                UserAccessTokens = new List<AccessToken>()
            });
        }

        public async Task<Account> LoginAccountAsync(LoginRequestMessage message)
        {
            var findOptions = Builders<Account>.Filter.And(
                Builders<Account>.Filter.Eq(a => a.UserEmail, message.UserEmail),
                Builders<Account>.Filter.Eq(a => a.UserPassword, message.UserPassword)
            );

            return await (await _accountCollection.FindAsync(findOptions)).FirstOrDefaultAsync();
        }

        public async Task<AccessToken> SaveAccessTokenAsync(string userEmail, AccessToken accessToken)
        {
            var findOption = Builders<Account>.Filter.Eq(a => a.UserEmail, userEmail);
            var updateOption = Builders<Account>.Update.Push(a => a.UserAccessTokens, accessToken);

            await _accountCollection.UpdateOneAsync(findOption, updateOption);

            return accessToken;
        }

        public async Task<Account> AuthenticateUserAsync(string userToken)
        {
            var findOption = Builders<Account>.Filter.And(
                Builders<Account>.Filter.Eq("userAccessTokens.Token", userToken),
                Builders<Account>.Filter.Gte("userAccessTokens.ExpiresAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            return await (await _accountCollection.FindAsync(findOption)).FirstOrDefaultAsync();
        }
    }
}