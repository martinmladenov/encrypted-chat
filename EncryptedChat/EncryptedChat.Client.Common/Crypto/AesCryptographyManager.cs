namespace EncryptedChat.Client.Common.Crypto
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public class AesCryptographyManager
    {
        private Aes aes;

        public string Encrypt(string text)
        {
            return Convert.ToBase64String(this.EncryptAsByteArray(text));
        }

        public byte[] EncryptAsByteArray(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] encrypted;

            using (var memoryStream = new MemoryStream())
            using (var cryptoStream = new CryptoStream(memoryStream,
                this.aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                using (var streamWriter = new StreamWriter(cryptoStream))
                {
                    streamWriter.Write(text);
                }

                encrypted = memoryStream.ToArray();
            }

            return encrypted;
        }

        public string Decrypt(string cipherText, string iv)
        {
            if (cipherText == null)
            {
                throw new ArgumentNullException(nameof(cipherText));
            }

            if (iv == null)
            {
                throw new ArgumentNullException(nameof(iv));
            }

            return this.Decrypt(Convert.FromBase64String(cipherText), Convert.FromBase64String(iv));
        }

        public string Decrypt(byte[] cipherText, byte[] iv)
        {
            if (cipherText == null)
            {
                throw new ArgumentNullException(nameof(cipherText));
            }

            if (iv == null)
            {
                throw new ArgumentNullException(nameof(iv));
            }

            this.aes.IV = iv;

            string plaintext;

            using (var memoryStream = new MemoryStream(cipherText))
            using (var cryptoStream = new CryptoStream(memoryStream, this.aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var streamReader = new StreamReader(cryptoStream))
            {
                plaintext = streamReader.ReadToEnd();
            }


            return plaintext;
        }

        public byte[] GenerateKey()
        {
            this.aes = Aes.Create();

            return this.aes.Key;
        }

        public void LoadKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.LoadKey(Convert.FromBase64String(key));
        }

        public void LoadKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.aes = Aes.Create();

            this.aes.Key = key;
        }

        public string ResetIv()
        {
            return Convert.ToBase64String(this.ResetIvAsByteArray());
        }

        public byte[] ResetIvAsByteArray()
        {
            this.aes.GenerateIV();

            return this.aes.IV;
        }
    }
}