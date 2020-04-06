namespace EncryptedChat.Client.Common.Configuration
{
    using System.Collections.Generic;

    public class MainConfiguration
    {
        public string Username { get; set; }

        public string ServerUrl { get; set; } = Constants.DefaultServerUrl;

        public string PrivateKey { get; set; }

        public IDictionary<string, string> TrustedUsers { get; set; }
            = new Dictionary<string, string>();
    }
}