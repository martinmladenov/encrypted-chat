namespace EncryptedChat.Server.Web.Services
{
    using Models;

    public interface IChatService
    {
        bool AddWaitingUser(string connectionId, string username, string publicKey);

        User[] GetWaitingUsers();

        string SetupConnectionToUser(string currUsername, string otherId, string currConnectionId,
            string key);

        User GetUserByConnectionId(string connectionId);

        string RemoveUserByConnectionId(string connectionId);

        bool IsWaiting(string connectionId);
    }
}