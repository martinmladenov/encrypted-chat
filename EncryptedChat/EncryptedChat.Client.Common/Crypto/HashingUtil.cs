namespace EncryptedChat.Client.Common.Crypto
{
    using System.Security.Cryptography;
    using System.Text;

    public static class HashingUtil
    {
        public static string GetSha256Hash(string str)
        {
            byte[] hashBytes;
            using (SHA256 sha256 = new SHA256Managed())
            {
                hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
            }

            var sb = new StringBuilder(64);
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
}