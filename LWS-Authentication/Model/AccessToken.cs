using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LWS_Authentication.Model
{
    public class AccessToken
    {
        public long CreatedAt { get; set; }
        public long ExpiresAt { get; set; }
        public string Token { get; set; }
    }
}