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
            custom
        }
        public string ApiKey = "";
        public string TTSApiKey = "";

        public AIProvider ProviderName = AIProvider.google;
        public string ModelName = "gemini-2.5-flash";
        public string Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        public bool TTSEnabled = true;
        public string TTSModelName = "gemini-2.5-flash-tts";
        public string TTSEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key=";
        public string PersonaText = "The player is running a colony and you are the storyteller controlling events. " +
            "An event just occurred. Respond in character in 2-4 sentences. Be dramatic. Address the player directly. " +
            "Do not use quotation marks around your response. Keep the narration concise, ideally under 100 words. " +
            "Use a tone that fits the event. If it's a minor event, be brief and lighthearted. " +
            "If it's a major crisis, be more serious and dramatic. Always relate it back to the colony's situation when possible. " +
            "Keep the reading on a third grade level. When talking to the player, do not refer to the player as 'Player' or 'Player Name'.";
        public float displayDuration = 15f;
        public float cooldownSeconds = 60f;
        public bool DebugLogging = false;
        public bool UseAccent = true;
        public bool UseEmotion = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref TTSApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ProviderName, "providerName", AIProvider.google);
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
            Scribe_Values.Look(ref DebugLogging, "DebugLogging", false);

            base.ExposeData();
        }
    }
}
