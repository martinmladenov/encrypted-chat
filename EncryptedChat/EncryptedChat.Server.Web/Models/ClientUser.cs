namespace EncryptedChat.Server.Web.Models
{
    public class ClientUser
    {
        public string Id { get; set; }

        // For backward compatibility
        public string ConnectionId => this.Id;

        public string Username { get; set; }

        public string PublicKey { get; set; }
    }
}