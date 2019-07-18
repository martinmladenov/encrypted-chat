namespace EncryptedChat.Client.App
{
    public static class Program
    {
        public static void Main()
        {
            var engine = new Engine();
            
            engine.Setup().GetAwaiter().GetResult();
        }
    }
}