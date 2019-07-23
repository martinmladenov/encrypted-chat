namespace EncryptedChat.Client.Common.Crypto
{
    public class EncryptedCommunicationsManager
    {
        private const char Delimiter = '-';

        private readonly RsaCryptographyManager otherRsa;
        private readonly RsaCryptographyManager ownRsa;
        private readonly AesCryptographyManager aes;

        public EncryptedCommunicationsManager()
        {
            this.otherRsa = new RsaCryptographyManager();
            this.ownRsa = new RsaCryptographyManager();
            this.aes = new AesCryptographyManager();
        }

        public string EncryptMessage(string message)
        {
            var iv = this.aes.ResetIv();
            var encrypted = this.aes.Encrypt(message);

            var encryptedData = iv + Delimiter + encrypted;

            return encryptedData;
        }

        public string DecryptMessage(string encryptedData)
        {
            var data = encryptedData.Split(Delimiter);
            var iv = data[0];
            var encryptedMessage = data[1];

            var decrypted = this.aes.Decrypt(encryptedMessage, iv);

            return decrypted;
        }

        public void GenerateNewRsaKey() => this.ownRsa.GenerateNewKey();

        public string ExportOwnRsaKey(bool includePrivate = false)
            => this.ownRsa.ExportKeyAsXml(includePrivate);

        public void ImportOwnRsaKey(string key)
        {
            this.ownRsa.LoadKeyFromXml(key);
        }

        public void ImportOtherRsaKey(string key)
        {
            this.otherRsa.LoadKeyFromXml(key);
        }

        public string GenerateEncryptedAesKey()
        {
            var aesKey = this.aes.GenerateKey();
            var encryptedKey = this.otherRsa.EncryptData(aesKey);
            return encryptedKey;
        }

        public string SignData(string data)
            => this.ownRsa.SignData(data);

        public bool VerifySignature(string data, string signature)
            => this.otherRsa.VerifySignature(data, signature);

        public void ImportEncryptedAesKey(string key)
        {
            var aes1KeyDec = this.ownRsa.DecryptDataAsByteArray(key);
            this.aes.LoadKey(aes1KeyDec);
        }

        public string GetOwnRsaFingerprint() => this.ownRsa.GetSha256Fingerprint();

        public string GetOtherRsaFingerprint() => this.otherRsa.GetSha256Fingerprint();
    }
}