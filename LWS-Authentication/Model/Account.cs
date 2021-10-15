using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LWS_Authentication.Model
{
    /// <summary>
    /// User model description. All about users!
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Account
    {
        /// <summary>
        /// Unique ID[Or Identifier] for Each User.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        
        /// <summary>
        /// User's Email Address
        /// </summary>
        [BsonElement("userEmail")]
        public string UserEmail { get; set; }

        /// <summary>
        /// User's Password Information. Note this should be encrypted.
        /// </summary>
        public string UserPassword { get; set; } // TODO: Need to be encrypted.
        
        /// <summary>
        /// User Access Tokens
        /// </summary>
        [BsonElement("userAccessTokens")]
        public List<AccessToken> UserAccessTokens { get; set; }

        public AccountProjection ToProjection() => new AccountProjection
        {
            UserEmail = this.UserEmail
        };
    }

    public class AccountProjection
    {
        public string UserEmail { get; set; }
    }
}