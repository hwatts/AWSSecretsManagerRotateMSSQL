using System;
using Newtonsoft.Json;
namespace RotateMssql
{
    public class SecretsEvent
    {
        public string SecretId;
        public string ClientRequestToken;
        public string Step;
    }

    public class SecretString
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("engine")]
        public string Engine { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("dbname")]
        public string DbName { get; set; }

    }
}
