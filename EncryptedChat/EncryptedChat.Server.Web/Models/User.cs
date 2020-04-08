namespace EncryptedChat.Server.Web.Models
{
    public class User
    {
        public string Id { get; set; }

        public string ConnectionId { get; set; }

        public string Username { get; set; }

        public string OtherUserConnectionId { get; set; }

        public string PublicKey { get; set; }

        public ClientUser ToClientUser()
        {
            return new ClientUser
            {
                Id = this.Id,
                Username = this.Username,
                PublicKey = this.PublicKey
            };
        }
    }
}