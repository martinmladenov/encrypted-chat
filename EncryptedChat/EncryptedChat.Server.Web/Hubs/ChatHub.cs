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

        private async Task UpdateClientWaitingList()
        {
            var freeUsers = this.chatService.GetWaitingUsers();

            await this.Clients.All.SendCoreAsync("UpdateWaitingList", new object[] {freeUsers});
        }

        public async Task ConnectToUser(string username, string otherConnectionId, string key)
        {
            var result = this.chatService.SetupConnectionToUser(
                username, otherConnectionId, this.Context.ConnectionId, key);

            if (!result)
            {
                return;
            }

            await this.Clients.Client(otherConnectionId)
                .SendCoreAsync("AcceptConnection", new object[] {key, username});

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
            var otherUserConnectionId = this.chatService.RemoveUserByConnectionId(this.Context.ConnectionId);

            if (otherUserConnectionId != null)
            {
                await this.Clients.Client(otherUserConnectionId).SendCoreAsync("Disconnect", new object[0]);

                this.chatService.RemoveUserByConnectionId(otherUserConnectionId);
            }

            await this.UpdateClientWaitingList();

            await base.OnDisconnectedAsync(exception);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            await this.UpdateClientWaitingList();
        }
    }
}