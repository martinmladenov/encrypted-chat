namespace EncryptedChat.Client.Common.Configuration
{
    using System.IO;
    using Newtonsoft.Json;

    public class ConfigurationManager<T>
        where T : new()
    {
        private readonly string configFilePath;

        public ConfigurationManager(string configFilePath)
        {
            this.configFilePath = configFilePath;

            this.ReloadConfiguration();
        }

        public T Configuration { get; private set; }

        public void ReloadConfiguration()
        {
            if (!File.Exists(this.configFilePath))
            {
                this.Configuration = new T();
                this.SaveChanges();
                return;
            }

            string configJson = File.ReadAllText(this.configFilePath);

            this.Configuration = JsonConvert.DeserializeObject<T>(configJson);
        }

        public void SaveChanges()
        {
            string newConfigJson = JsonConvert.SerializeObject(this.Configuration);

            File.WriteAllText(this.configFilePath, newConfigJson);
        }
    }
}