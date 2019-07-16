namespace EncryptedChat.Server.Web.Services.Implementations
{
    using System.Collections.Generic;
    using System.Linq;
    using Models;

    public class ChatService : IChatService
    {
        private readonly HashSet<User> users;

        public ChatService()
        {
            this.users = new HashSet<User>();
        }

        public bool AddWaitingUser(string connectionId, string username, string publicKey)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(publicKey) ||
                this.users.Any(u => u.Username == username))
            {
                return false;
            }

            this.users.Add(new User
            {
                ConnectionId = connectionId, Username = username, PublicKey = publicKey
            });

            return true;
        }

        public User[] GetWaitingUsers()
        {
            var freeUsers = this.users.Where(u => u.OtherUserConnectionId == null).ToArray();

            return freeUsers;
        }

        public bool SetupConnectionToUser(string currUsername, string otherConnectionId, string currConnectionId,
            string key)
        {
            if (string.IsNullOrWhiteSpace(currUsername) ||
                string.IsNullOrWhiteSpace(otherConnectionId) ||
                string.IsNullOrWhiteSpace(currConnectionId) ||
                string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var otherUser = this.users.SingleOrDefault(u => u.ConnectionId == otherConnectionId);

            if (otherUser == null || otherUser.OtherUserConnectionId != null)
            {
                return false;
            }

            var currUser = new User {ConnectionId = currConnectionId, Username = currUsername};

            this.users.Add(currUser);

            otherUser.OtherUserConnectionId = currUser.ConnectionId;
            currUser.OtherUserConnectionId = otherConnectionId;

            return true;
        }

        public User GetUserByConnectionId(string connectionId)
        {
            if (connectionId == null)
            {
                return null;
            }

            return this.users.SingleOrDefault(u => u.ConnectionId == connectionId);
        }

        public string RemoveUserByConnectionId(string connectionId)
        {
            if (connectionId == null)
            {
                return null;
            }

            var user = this.users.SingleOrDefault(u => u.ConnectionId == connectionId);

            if (user == null)
            {
                return null;
            }

            this.users.Remove(user);

            string otherConnectionId = user.OtherUserConnectionId;

            if (otherConnectionId == null)
            {
                return null;
            }

            var otherUser = this.users.SingleOrDefault(u => u.ConnectionId == otherConnectionId);

            if (otherUser != null)
            {
                otherUser.OtherUserConnectionId = null;
            }

            return otherConnectionId;
        }
    }
}