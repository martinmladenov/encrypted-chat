namespace EncryptedChat.Server.Web.Hubs
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Services;

    public class ChatHub : Hub
    {
        private readonly IChatService chatService;

        public ChatHub(IChatService chatService)
        {
            this.chatService = chatService;
        }

        public async Task RegisterAsWaiting(string username, string publicKey)
        {
            bool result = this.chatService.AddWaitingUser(this.Context.ConnectionId, username, publicKey);

            if (!result)
            {
                return;
            }

            await this.UpdateClientWaitingList();
        }

        private async Task UpdateClientWaitingList(string recipientId = null)
        {
            var freeUsers = this.chatService.GetWaitingUsers();

            var recipient = recipientId == null ? this.Clients.All : this.Clients.Client(recipientId);

            await recipient.SendCoreAsync("UpdateWaitingList", new object[] {freeUsers});
        }

        public async Task ConnectToUser(string username, string otherConnectionId, string aesKey,
            string rsaKey, string signature)
        {
            var result = this.chatService.SetupConnectionToUser(
                username, otherConnectionId, this.Context.ConnectionId, aesKey);

            if (!result)
            {
                return;
            }

            await this.Clients.Client(otherConnectionId)
                .SendCoreAsync("AcceptConnection", new object[] {aesKey, username, rsaKey, signature});

            await this.UpdateClientWaitingList();
        }

        public async Task SendMessage(string encryptedMessage)
        {
            var user = this.chatService.GetUserByConnectionId(this.Context.ConnectionId);

            if (user == null || user.OtherUserConnectionId == null)
            {
                return;
            }

            await this.Clients.Client(user.OtherUserConnectionId)
                .SendCoreAsync("NewMessage", new object[] {encryptedMessage, user.Username});
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            bool isWaiting = this.chatService.IsWaiting(this.Context.ConnectionId);

            var otherUserConnectionId = this.chatService.RemoveUserByConnectionId(this.Context.ConnectionId);

            if (otherUserConnectionId != null)
            {
                await this.Clients.Client(otherUserConnectionId).SendCoreAsync("Disconnect", new object[0]);
            }

            if (isWaiting)
            {
                await this.UpdateClientWaitingList();
            }

            await base.OnDisconnectedAsync(exception);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            await this.UpdateClientWaitingList(this.Context.ConnectionId);
        }
    }
}