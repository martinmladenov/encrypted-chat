namespace EncryptedChat.Client.Common.Crypto
{
    public class EncryptedCommunicationsManager
    {
        private const char Delimiter = '-';

        private readonly RsaCryptographyManager rsa;
        private readonly AesCryptographyManager aes;

        public EncryptedCommunicationsManager()
        {
            this.rsa = new RsaCryptographyManager();
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

        public void GenerateNewRsaKey() => this.rsa.GenerateNewKey();

        public string ExportRsaKey(bool includePrivate = false)
            => this.rsa.ExportKeyAsXml(includePrivate);

        public void ImportRsaKey(string key)
        {
            this.rsa.LoadKeyFromXml(key);
        }

        public string GenerateEncryptedAesKey()
        {
            var aesKey = this.aes.GenerateKey();
            var encryptedKey = this.rsa.EncryptData(aesKey);
            return encryptedKey;
        }

        public void ImportEncryptedAesKey(string key)
        {
            var aes1KeyDec = this.rsa.DecryptDataAsByteArray(key);
            this.aes.LoadKey(aes1KeyDec);
        }

        public string GetRsaFingerprint() => this.rsa.GetSha256Fingerprint();
    }
}