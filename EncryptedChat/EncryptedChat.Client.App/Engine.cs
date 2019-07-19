namespace EncryptedChat.Client.App
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Common.Crypto;
    using Microsoft.AspNetCore.SignalR.Client;
    using Models;

    public class Engine
    {
        private HubConnection connection;
        private EncryptedCommunicationsManager communicationsManager;

        private User[] waitingUsers;
        private string username;
        private State state;

        private async Task SetUpConnection()
        {
            this.communicationsManager = new EncryptedCommunicationsManager();

            Console.WriteLine(Messages.ConnectingToServer, Constants.ServerUrl);

            this.connection = new HubConnectionBuilder()
                .WithUrl(Constants.ServerUrl)
                .Build();

            this.connection.On<User[]>(nameof(this.UpdateWaitingList), this.UpdateWaitingList);
            this.connection.On<string, string>(nameof(this.AcceptConnection), this.AcceptConnection);
            this.connection.On<string, string>(nameof(this.NewMessage), this.NewMessage);
            this.connection.On(nameof(this.Disconnect), this.Disconnect);

            await this.connection.StartAsync();

            Console.WriteLine(Messages.Connected);
        }

        private static string GetUsername()
        {
            string input;

            do
            {
                Console.Write(Messages.UsernamePrompt);
                input = Console.ReadLine();
            } while (string.IsNullOrWhiteSpace(input));

            return input;
        }

        public async Task Setup()
        {
            this.username = GetUsername();

            await this.SetUpConnection();

            this.state = State.SelectingUser;

            await this.StartReadingInput();
        }

        private async Task StartReadingInput()
        {
            while (true)
            {
                string input = Console.ReadLine();

                if (input == Constants.ExitCommand ||
                    this.connection.State == HubConnectionState.Disconnected ||
                    this.state == State.Disconnected)
                {
                    Console.WriteLine(Messages.Disconnected);

                    if (this.connection.State != HubConnectionState.Disconnected)
                    {
                        await this.connection.StopAsync();
                    }

                    break;
                }

                switch (this.state)
                {
                    case State.SelectingUser:
                        await this.UserSelect(input);
                        break;
                    case State.InChat:
                        await this.SendMessage(input);
                        break;
                }
            }
        }

        private async Task SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            message = message.Trim();

            var encryptedMessage = this.communicationsManager.EncryptMessage(message);

            await this.connection.InvokeCoreAsync("SendMessage", new object[]
            {
                encryptedMessage
            });
        }

        private async Task UserSelect(string input)
        {
            if (this.waitingUsers == null)
            {
                return;
            }

            if (!int.TryParse(input, out int selected) ||
                selected < 0 ||
                selected > this.waitingUsers.Length)
            {
                Console.WriteLine(Messages.InvalidUserIdSelectedError, this.waitingUsers.Length);
                return;
            }

            if (selected == 0)
            {
                await this.JoinAsWaitingUser();
                return;
            }

            var selectedUser = this.waitingUsers[selected - 1];

            await this.ConnectWithUser(selectedUser);
        }

        private async Task ConnectWithUser(User selectedUser)
        {
            this.state = State.InChat;

            Console.Clear();
            Console.WriteLine(Messages.GeneratingSessionKey);

            this.communicationsManager.ImportRsaKey(selectedUser.PublicKey);
            string aesKey = this.communicationsManager.GenerateEncryptedAesKey();

            Console.WriteLine(Messages.InitialisingEncryptedConnection);

            await this.connection.InvokeCoreAsync("ConnectToUser", new object[]
            {
                this.username, selectedUser.ConnectionId, aesKey
            });

            Console.WriteLine();
            Console.WriteLine(Messages.ConnectedWithUser, selectedUser.Username);
            Console.WriteLine();
            Console.WriteLine(Messages.KeyFingerprint, this.communicationsManager.GetRsaFingerprint());
            Console.WriteLine();
        }

        private void UpdateWaitingList(User[] users)
        {
            if (this.state != State.SelectingUser)
            {
                return;
            }

            this.waitingUsers = users;

            Console.WriteLine();
            Console.WriteLine(Messages.UserListHeader);

            if (!users.Any())
            {
                Console.WriteLine(Messages.UserListNoUsers);
            }
            else
            {
                for (int i = 0; i < users.Length; i++)
                {
                    Console.WriteLine(Messages.UserListItem, i + 1, users[i].Username);
                }
            }

            Console.WriteLine(Messages.UserListItem, 0, Messages.UserListJoin);
        }

        private async Task JoinAsWaitingUser()
        {
            Console.WriteLine(Messages.GeneratingKeyPair);

            string pubKey = this.communicationsManager.GenerateRsaKey();

            Console.WriteLine(Messages.SendingKeyToServer);

            this.state = State.Waiting;

            await this.connection.InvokeCoreAsync("RegisterAsWaiting", new object[]
            {
                this.username, pubKey
            });

            Console.Clear();

            Console.WriteLine(Messages.WaitingForUser);
        }

        private void AcceptConnection(string key, string otherUsername)
        {
            if (this.state != State.Waiting)
            {
                return;
            }

            Console.WriteLine(Messages.InitialisingEncryptedConnection);

            this.communicationsManager.ImportEncryptedAesKey(key);

            this.state = State.InChat;

            Console.WriteLine();
            Console.WriteLine(Messages.ConnectedWithUser, otherUsername);
            Console.WriteLine();
            Console.WriteLine(Messages.KeyFingerprint, this.communicationsManager.GetRsaFingerprint());
            Console.WriteLine();
        }

        private void NewMessage(string encryptedMessage, string messageUsername)
        {
            if (this.state != State.InChat)
            {
                return;
            }

            string decryptedMessage = this.communicationsManager.DecryptMessage(encryptedMessage);

            Console.WriteLine(Messages.MessageFormat, messageUsername, decryptedMessage);
        }

        private void Disconnect()
        {
            Console.WriteLine(Messages.Disconnected);

            this.state = State.Disconnected;
        }
    }
}