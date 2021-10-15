using LWS_Authentication.Configuration;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace LWS_Authentication.Repository
{
    /// <summary>
    /// Mongo Context. Should be registered with 'Singleton' Object.
    /// </summary>
    public class MongoContext
    {
        /// <summary>
        /// Mongo Client object, can access whole db or cluster itself.
        /// </summary>
        public readonly MongoClient MongoClient;
        
        /// <summary>
        /// Mongo Database Object, responsible for database itself.
        /// </summary>
        public readonly IMongoDatabase MongoDatabase;
        
        public MongoContext(MongoConfiguration mongoConfiguration)
        {
            MongoClient = new MongoClient(mongoConfiguration.MongoConnection);
            MongoDatabase = MongoClient.GetDatabase(mongoConfiguration.MongoDbName);
        }
    }
}