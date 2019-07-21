namespace EncryptedChat.Client.App
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Common.Configuration;
    using Common.Crypto;
    using Microsoft.AspNetCore.SignalR.Client;
    using Models;

    public class Engine
    {
        private HubConnection connection;
        private EncryptedCommunicationsManager communicationsManager;
        private ConfigurationManager<MainConfiguration> configurationManager;

        private User[] waitingUsers;
        private string username;
        private State state;
        private User otherUser;

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

        private void LoadUsername()
        {
            this.username = this.configurationManager.Configuration.Username;

            if (!string.IsNullOrWhiteSpace(this.username))
            {
                return;
            }

            do
            {
                Console.Write(Messages.UsernamePrompt);
                this.username = Console.ReadLine();
            } while (string.IsNullOrWhiteSpace(this.username));

            this.configurationManager.Configuration.Username = this.username;
            this.configurationManager.SaveChanges();
        }

        private void LoadConfiguration()
        {
            Console.WriteLine(Messages.LoadingConfiguration);

            this.configurationManager = new ConfigurationManager<MainConfiguration>(Constants.ConfigurationFilePath);

            this.LoadUsername();
        }

        public async Task Setup()
        {
            this.LoadConfiguration();

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
                        if (input == Constants.TrustCommand)
                        {
                            this.TrustCurrentUser();
                            break;
                        }

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

            this.otherUser = selectedUser;

            string trustedBadge = this.IsUserTrusted(this.otherUser)
                ? Messages.UserTrustedBadge
                : Messages.UserNotTrustedBadge;

            Console.WriteLine();
            Console.WriteLine(Messages.ConnectedWithUser, selectedUser.Username, trustedBadge);
            Console.WriteLine();
            Console.WriteLine(Messages.KeyFingerprint, this.communicationsManager.GetRsaFingerprint());
            Console.WriteLine();
        }

        private bool IsUserTrusted(User user)
        {
            if (!this.configurationManager.Configuration.TrustedUsers.ContainsKey(user.Username))
            {
                return false;
            }

            string keyHash = HashingUtil.GetSha256Hash(user.PublicKey);

            return this.configurationManager.Configuration.TrustedUsers[user.Username] == keyHash;
        }

        private void TrustCurrentUser()
        {
            bool result = this.TrustUser(this.otherUser);

            Console.WriteLine(result ? Messages.UserTrusted : Messages.CouldNotTrustUser);
        }

        private bool TrustUser(User user)
        {
            if (user == null)
            {
                return false;
            }

            if (this.configurationManager.Configuration.TrustedUsers.ContainsKey(user.Username))
            {
                return false;
            }

            string keyHash = HashingUtil.GetSha256Hash(user.PublicKey);

            this.configurationManager.Configuration.TrustedUsers.Add(user.Username, keyHash);
            
            this.configurationManager.SaveChanges();

            return true;
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
            this.LoadPrivateKey();

            string pubKey = this.communicationsManager.ExportRsaKey();

            Console.WriteLine(Messages.SendingKeyToServer);

            this.state = State.Waiting;

            await this.connection.InvokeCoreAsync("RegisterAsWaiting", new object[]
            {
                this.username, pubKey
            });

            Console.Clear();

            Console.WriteLine(Messages.WaitingForUser);
        }

        private void LoadPrivateKey()
        {
            if (this.configurationManager.Configuration.PrivateKey == null)
            {
                Console.WriteLine(Messages.GeneratingKeyPair);

                this.communicationsManager.GenerateNewRsaKey();

                this.configurationManager.Configuration.PrivateKey =
                    this.communicationsManager.ExportRsaKey(true);

                this.configurationManager.SaveChanges();
            }
            else
            {
                Console.WriteLine(Messages.LoadingPrivateKey);

                this.communicationsManager.ImportRsaKey(this.configurationManager.Configuration.PrivateKey);
            }
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
            Console.WriteLine(Messages.ConnectedWithUser, otherUsername, Messages.UserNotTrustedBadge); // TODO 
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