namespace EncryptedChat.Client.App
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Common;
    using Common.Configuration;
    using Common.Crypto;
    using Microsoft.AspNetCore.SignalR.Client;
    using Common.Models;

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
            Console.WriteLine(Messages.ConnectingToServer, this.configurationManager.Configuration.ServerUrl);

            this.connection = new HubConnectionBuilder()
                .WithUrl(this.configurationManager.Configuration.ServerUrl)
                .Build();

            this.connection.On<User[]>(nameof(this.UpdateWaitingList), this.UpdateWaitingList);
            this.connection.On<string, string, string, string>(nameof(this.AcceptConnection), this.AcceptConnection);
            this.connection.On<string, string>(nameof(this.NewMessage), this.NewMessage);
            this.connection.On(nameof(this.Disconnect), this.Disconnect);

            await this.connection.StartAsync();

            Console.WriteLine(Messages.Connected);
        }

        private void LoadUsername()
        {
            this.username = this.configurationManager.Configuration.Username;

            Regex usernameRegex = new Regex(Constants.UsernameRegex);

            if (!string.IsNullOrWhiteSpace(this.username) && usernameRegex.IsMatch(this.username))
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine(Messages.UsernameInfo);
            Console.WriteLine();

            do
            {
                Console.Write(Messages.UsernamePrompt);
                this.username = Console.ReadLine();
            } while (string.IsNullOrWhiteSpace(this.username) || !usernameRegex.IsMatch(this.username));

            this.configurationManager.Configuration.Username = this.username;
            this.configurationManager.SaveChanges();
        }

        private void LoadConfiguration()
        {
            Console.WriteLine(Messages.LoadingConfiguration);

            this.configurationManager = new ConfigurationManager<MainConfiguration>(Constants.ConfigurationFilePath);

            this.LoadUsername();

            this.communicationsManager = new EncryptedCommunicationsManager();

            this.LoadPrivateKey();
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

                if (input == Commands.ExitCommand ||
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
                        if (input == Commands.TrustCommand)
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
            Console.Clear();
            Console.WriteLine(Messages.GeneratingSessionKey);

            this.communicationsManager.ImportOtherRsaKey(selectedUser.PublicKey);
            string aesKey = this.communicationsManager.GenerateEncryptedAesKey();
            string key = this.communicationsManager.ExportOwnRsaKey();
            string signature = this.communicationsManager.SignData(aesKey);

            Console.WriteLine(Messages.InitialisingEncryptedConnection);

            await this.connection.InvokeCoreAsync("ConnectToUser", new object[]
            {
                this.username, selectedUser.Id, aesKey, key, signature
            });

            this.CreateChatWithUser(selectedUser);
        }

        private void CreateChatWithUser(User user)
        {
            this.state = State.InChat;

            this.otherUser = user;

            bool isTrusted = this.IsUserTrusted(this.otherUser);

            string trustedBadge = isTrusted
                ? Messages.UserTrustedBadge
                : Messages.UserNotTrustedBadge;

            Console.WriteLine();
            Console.WriteLine(Messages.ConnectedWithUser, user.Username, trustedBadge);
            Console.WriteLine();
            Console.WriteLine(Messages.CurrentUserFingerprint, this.communicationsManager.GetOwnRsaFingerprint());
            Console.WriteLine();

            if (!isTrusted)
            {
                Console.WriteLine(Messages.OtherUserFingerprint, this.otherUser.Username,
                    this.communicationsManager.GetOtherRsaFingerprint());
                Console.WriteLine();

                Console.WriteLine(new string('-', 30));
                Console.WriteLine();
                Console.WriteLine(Messages.UserNotTrustedMessage);
                Console.WriteLine();
                Console.WriteLine(new string('-', 30));
                Console.WriteLine();
            }
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

            Regex usernameRegex = new Regex(Constants.UsernameRegex);

            this.waitingUsers = users.Where(user =>
                    !string.IsNullOrWhiteSpace(user.Username) &&
                    usernameRegex.IsMatch(user.Username))
                .ToArray();

            int invalidUsernamesDifference = users.Length - this.waitingUsers.Length;

            Console.WriteLine();
            Console.WriteLine(Messages.UserListHeader);

            if (this.waitingUsers.Length == 0)
            {
                Console.WriteLine(Messages.UserListNoUsers);
            }
            else
            {
                for (int i = 0; i < this.waitingUsers.Length; i++)
                {
                    string trustedBadge = this.IsUserTrusted(this.waitingUsers[i])
                        ? Messages.UserTrustedBadge
                        : Messages.UserNotTrustedBadge;

                    Console.WriteLine(Messages.UserListItem, i + 1, this.waitingUsers[i].Username, trustedBadge);
                }
            }

            if (invalidUsernamesDifference != 0)
            {
                Console.WriteLine(Messages.UserListInvalidUsername, invalidUsernamesDifference,
                    invalidUsernamesDifference != 1 ? "s" : "");
            }

            Console.WriteLine(Messages.UserListJoin);
        }

        private async Task JoinAsWaitingUser()
        {
            string pubKey = this.communicationsManager.ExportOwnRsaKey();

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
                    this.communicationsManager.ExportOwnRsaKey(true);

                this.configurationManager.SaveChanges();
            }
            else
            {
                Console.WriteLine(Messages.LoadingPrivateKey);

                this.communicationsManager.ImportOwnRsaKey(this.configurationManager.Configuration.PrivateKey);
            }
        }

        private void AcceptConnection(string aesKey, string otherUsername, string rsaKey, string signature)
        {
            if (this.state != State.Waiting)
            {
                return;
            }

            Console.WriteLine(Messages.InitialisingEncryptedConnection);

            this.communicationsManager.ImportOtherRsaKey(rsaKey);
            var signatureValid = this.communicationsManager.VerifySignature(aesKey, signature);
            if (!signatureValid)
            {
                Console.WriteLine(Messages.IncomingConnectionSignatureInvalid);
                this.Disconnect();
                return;
            }

            this.communicationsManager.ImportEncryptedAesKey(aesKey);

            var user = new User
            {
                Username = otherUsername,
                PublicKey = rsaKey
            };

            this.CreateChatWithUser(user);
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