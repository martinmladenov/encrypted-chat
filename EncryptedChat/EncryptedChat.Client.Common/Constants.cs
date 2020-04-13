namespace EncryptedChat.Client.Common
{
    public static class Constants
    {
        public const string ConfigurationFilePath = "encrypted-chat-config.json";
        public const string DefaultServerUrl = "https://ench.azurewebsites.net/chat";
        public const string UsernameRegex = @"^(?=.{3,20}$)(?![_.])(?!.*[_.]{2})[a-zA-Z0-9._]+(?<![_.])$";
    }
}