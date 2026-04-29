
namespace LivingStoryteller
{
    internal static class AIProviderFactory 
    {
        public static Task<string> GetResponse(string content)
        {
            var provider = ModOptions.Settings.ProviderName;
            return GetAIProvider(provider).GetResponse(content);
        }

        public static Task<string> GetTTSResponse(string content)
        {
            var provider = ModOptions.Settings.TTSProviderName;
            return GetAIProvider(provider).GetTTSResponse(content);
        }

        public static string JSONRequest(string model, string systemPrompt, string userMessage)
        {
            var provider = ModOptions.Settings.ProviderName;
            return GetAIProvider(provider).JSONRequest(model, systemPrompt, userMessage);
        }

        public static string JSONTTSRequest(string text, string personaDef, string voice, string emotion, string mood)
        {
            var provider = ModOptions.Settings.TTSProviderName;
            return GetAIProvider(provider).JSONTTSRequest(text, personaDef, voice, emotion, mood);
        }

        private static IAIProvider GetAIProvider(StorytellerSettings.AIProvider provider)
        {
            switch (provider)
            {
                case StorytellerSettings.AIProvider.open_ai:
                    return new OpenAIProvider();
                case StorytellerSettings.AIProvider.player2:
                    return new Player2Provider();
                case StorytellerSettings.AIProvider.google:
                    return new GoogleProvider();
                default:
                    throw new NotImplementedException($"AI Provider {provider} not implemented");
            }
        }
    }
}
