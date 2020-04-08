namespace EncryptedChat.Server.Web.Models
{
    public class User
    {
        public string ConnectionId { get; set; }

        public string Username { get; set; }

        public string OtherUserConnectionId { get; set; }

        public string PublicKey { get; set; }
    }
}