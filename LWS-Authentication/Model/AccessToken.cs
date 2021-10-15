using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LWS_Authentication.Model
{
    public class AccessToken
    {
        [BsonRepresentation(BsonType.Document)]
        public DateTime CreatedAt { get; set; }
        [BsonRepresentation(BsonType.Document)]
        public DateTime ExpiresAt { get; set; }
        public string Token { get; set; }
    }
}