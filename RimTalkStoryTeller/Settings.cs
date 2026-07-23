using RimWorld;
using Verse;

namespace LivingStoryteller
{
    public class StorytellerSettings : ModSettings
    {
        public enum AIProvider
        {
            google,
            open_ai,
            player2,
            novel_ai,
            custom
        }

        public List<string> Storytellers = new List<string>();
        public string ApiKey = "";
        public string TTSApiKey = "";
        public bool GameLoaded = false;

        public AIProvider ProviderName = AIProvider.google;
        public AIProvider TTSProviderName = AIProvider.google;
        public string ModelName = "gemini-2.5-flash";
        public string Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        public bool TTSEnabled = true;
        public string TTSModelName = "gemini-2.5-flash-tts";
        public string TTSEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key=";
        public string PersonaText = "The player is running a colony and you are the storyteller controlling events. " +
            "An event just occurred. Respond in character in 2-4 sentences. Address the player directly. " +
            "Do not use quotation marks around your response. Keep the narration concise, ideally under 100 words. " +
            "Always relate it back to the colony's situation when possible. " +
            "Keep the reading on a third grade level. When talking to the player, do not refer to the player as 'Player' or 'Player Name'.";
        public float displayDuration = 15f;
        public float cooldownSeconds = 60f;
        public bool SkipEventsDuringCooldown = false;
        public bool EnableEchoTalesIntegration = true;
        public bool EchoTalesReadEveryNewEntry = false;
        public bool DebugLogging = false;
        public bool UseAccent = true;
        public bool UseEmotion = true;
        public float Stress = 0f;        // rises with disasters, starvation, injuries
        public float Chaos = 0f;         // rises with raids, threats, explosions
        public float Sympathy = 0f;      // rises with pawn deaths, mental breaks
        public float Confidence = 0f;    // rises with wealth, victories, growth

        public string EffectiveTTSApiKey => string.IsNullOrWhiteSpace(TTSApiKey) ? ApiKey : TTSApiKey;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref TTSApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ProviderName, "providerName", AIProvider.google);
            Scribe_Values.Look(ref TTSProviderName, "ttsProviderName", AIProvider.google);
            Scribe_Values.Look(ref ModelName, "modelName", "");
            Scribe_Values.Look(ref Endpoint, "endpoint", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
            Scribe_Values.Look(ref TTSEnabled, "TTSEnabled", true);
            Scribe_Values.Look(ref TTSModelName, "ttsModelName", "gemini-2.5-flash-preview-tts");
            Scribe_Values.Look(ref TTSEndpoint, "ttsEndpoint", "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key=");
            Scribe_Values.Look(ref PersonaText, "personaText", "The player is running a colony and you are the storyteller controlling events. " +
            "An event just occurred. Respond in character in 2-4 sentences. Be dramatic. Address the player directly. " +
            "Do not use quotation marks around your response. Keep the narration concise, ideally under 100 words. " +
            "Use a tone that fits the event. If it's a minor event, be brief and lighthearted. " +
            "If it's a major crisis, be more serious and dramatic. Always relate it back to the colony's situation when possible. " +
            "Keep the reading on a third grade level. When talking to the player, do not refer to the player as 'Player' or 'Player Name'.");
            Scribe_Values.Look(ref displayDuration, "displayDuration", 15f);
            Scribe_Values.Look(ref cooldownSeconds, "cooldownSeconds", 60f);
            Scribe_Values.Look(ref SkipEventsDuringCooldown, "skipEventsDuringCooldown", false);
            Scribe_Values.Look(ref EnableEchoTalesIntegration, "enableEchoTalesIntegration", true);
            Scribe_Values.Look(ref EchoTalesReadEveryNewEntry, "echoTalesReadEveryNewEntry", false);
            Scribe_Values.Look(ref DebugLogging, "DebugLogging", true);
            Scribe_Values.Look(ref UseAccent, "UseAccent", true);
            Scribe_Values.Look(ref UseEmotion, "UseEmotion", true);
            Scribe_Values.Look(ref Stress, "Stress", 0f);
            Scribe_Values.Look(ref Chaos, "Chaos", 0f);
            Scribe_Values.Look(ref Sympathy, "Sympathy", 0f);
            Scribe_Values.Look(ref Confidence, "Confidence", 0f);

            base.ExposeData();
        }
    }
}
