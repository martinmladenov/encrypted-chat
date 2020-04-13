namespace EncryptedChat.Server.Web.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
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
                !Regex.IsMatch(username, Constants.UsernameRegex) ||
                string.IsNullOrWhiteSpace(publicKey) ||
                this.users.Any(u => u.Username == username))
            {
                return false;
            }

            this.users.Add(new User
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId, Username = username, PublicKey = publicKey
            });

            return true;
        }

        public User[] GetWaitingUsers()
        {
            var freeUsers = this.users.Where(u => u.OtherUserConnectionId == null).ToArray();

            return freeUsers;
        }

        public string SetupConnectionToUser(string currUsername, string otherId, string currConnectionId,
            string key)
        {
            if (string.IsNullOrWhiteSpace(currUsername) ||
                !Regex.IsMatch(currUsername, Constants.UsernameRegex) ||
                string.IsNullOrWhiteSpace(otherId) ||
                string.IsNullOrWhiteSpace(currConnectionId) ||
                string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var otherUser = this.users.SingleOrDefault(u => u.Id == otherId);

            if (otherUser == null || otherUser.OtherUserConnectionId != null)
            {
                return null;
            }

            var currUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = currConnectionId, Username = currUsername
            };

            this.users.Add(currUser);

            otherUser.OtherUserConnectionId = currUser.ConnectionId;
            currUser.OtherUserConnectionId = otherUser.ConnectionId;

            return otherUser.ConnectionId;
        }

        public User GetUserByConnectionId(string connectionId)
        {
            if (connectionId == null)
            {
                return null;
            }

            return this.users.SingleOrDefault(u => u.ConnectionId == connectionId);
        }

        public bool IsWaiting(string connectionId)
        {
            var user = this.users.SingleOrDefault(u => u.ConnectionId == connectionId);

            if (user == null)
            {
                return false;
            }

            return user.OtherUserConnectionId == null;
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
                this.users.Remove(otherUser);
            }

            return otherConnectionId;
        }
    }
}