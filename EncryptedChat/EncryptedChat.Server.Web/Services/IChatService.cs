namespace EncryptedChat.Server.Web.Services
{
    using Models;

    public interface IChatService
    {
        bool AddWaitingUser(string connectionId, string username, string publicKey);

        User[] GetWaitingUsers();

        bool SetupConnectionToUser(string currUsername, string otherConnectionId, string currConnectionId,
            string key);

        User GetUserByConnectionId(string connectionId);

        string RemoveUserByConnectionId(string connectionId);
    }
}