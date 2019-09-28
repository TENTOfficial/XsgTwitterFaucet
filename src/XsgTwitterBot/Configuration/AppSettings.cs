namespace XsgTwitterBot.Configuration
{
    public class AppSettings
    {
        public string LogServerUrl { get; set; }

        public NodeOptions NodeOptions { get; set; }

        public TwitterSettings TwitterSettings { get; set; }

        public BotSettings BotSettings { get; set; }

        public StatSettings StatSettings { get; set; }
        public int ProcessingFrequency { get; set; }  = 120000;
    }
}