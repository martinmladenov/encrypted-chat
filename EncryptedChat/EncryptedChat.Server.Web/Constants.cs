namespace EncryptedChat.Server.Web
{
    public static class Constants
    {
        public const string RedirectUrl = "https://github.com/martinmladenov/encrypted-chat";
        public const string UsernameRegex = @"^(?=.{3,20}$)(?![_.])(?!.*[_.]{2})[a-zA-Z0-9._]+(?<![_.])$";
    }
}