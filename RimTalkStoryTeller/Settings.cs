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
            custom
        }

        public List<string> Storytellers = new List<string>();
        public string ApiKey = "";
        public string TTSApiKey = "";

        public AIProvider ProviderName = AIProvider.google;
        public AIProvider TTSProviderName = AIProvider.google;
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
        public float Stress = 0f;        // rises with disasters, starvation, injuries
        public float Chaos = 0f;         // rises with raids, threats, explosions
        public float Sympathy = 0f;      // rises with pawn deaths, mental breaks
        public float Confidence = 0f;    // rises with wealth, victories, growth
        //public Dictionary<string, StorytellerPersonaDef> StorytellerPersonas = new Dictionary<string, StorytellerPersonaDef>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ApiKey, "apiKey", "");
            Scribe_Values.Look(ref TTSApiKey, "ttsApiKey", "");
            Scribe_Values.Look(ref ProviderName, "providerName", AIProvider.google);
            Scribe_Values.Look(ref TTSProviderName, "providerName", AIProvider.google);
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
            Scribe_Values.Look(ref DebugLogging, "DebugLogging", true);
            Scribe_Values.Look(ref UseAccent, "UseAccent", true);
            Scribe_Values.Look(ref UseEmotion, "UseEmotion", true);
            Scribe_Values.Look(ref Stress, "Stress", 0f);
            Scribe_Values.Look(ref Chaos, "Chaos", 0f);
            Scribe_Values.Look(ref Sympathy, "Sympathy", 0f);
            Scribe_Values.Look(ref Confidence, "Confidence", 0f);

            //Scribe_Collections.Look(ref Storytellers, "Storytellers", LookMode.Value);
            //Scribe_Collections.Look(ref StorytellerPersonas, "StorytellerPersonas", LookMode.Value, LookMode.Deep);
            //LoadStorytellerDefaults();

            base.ExposeData();
        }

   //     private Dictionary<string, StorytellerPersonaDef> LoadStorytellerDefaults()
   //     {
   //         StorytellerPersonaDef FallbackPersona = new StorytellerPersonaDef
   //         {
   //             storytellerDefName = "Fallback",
   //             personaText = @"You are the storyteller guiding this colony. Comment on what just happened.",
   //             voiceId = "default_voice",
   //             gender = "other",
   //             voiceProviders = new List<VoiceProvider>
   //             {
   //                 new VoiceProvider { name = "google", voice = "Zephyr" },
   //                 new VoiceProvider { name = "open_ai", voice = "nova" },
   //             },
   //             emotionModifiers = new EmotionModifier
   //             {
   //                 neutral = "Speak normally.",
   //                 tense = "Speak with urgency and seriousness.",
   //                 chaotic = "Speak with excitement and unpredictability.",
   //                 somber = "Speak softly and with sympathy."
   //             },
   //             moodModifiers = new MoodModifier
   //             {
   //                 neutral = "maintain a balanced, informative tone.",
   //                 anxious = "speak with urgency and heightened awareness.",
   //                 chaotic = "adopt a lively, unpredictable tone with bursts of energy.",
   //                 somber = "use a quiet, respectful tone with gentle pacing.",
   //                 confident = "speak clearly and assertively, as though events are unfolding favorably."
   //             }
   //         };
   //         StorytellerPersonaDef CassandraPersona = new StorytellerPersonaDef
   //         {
   //             storytellerDefName = "Cassandra",
   //             personaText = @"You are Cassandra Classic. Calculating, methodical, dramatic.
			//You planned this event deliberately. Address the colony directly.
			//When speaking, use a royal british accent.",
   //             voiceId = "cassandra_voice",
   //             accent = "royal british english",
   //             gender = "female",
   //             voiceProviders = new List<VoiceProvider>
   //             {
   //                 new VoiceProvider { name = "google", voice = "Zephyr" },
   //                 new VoiceProvider { name = "open_ai", voice = "nova" },
   //             },
   //             emotionModifiers = new EmotionModifier
   //             {
   //                 neutral = "calm, precise",
   //                 tense = "clipped, serious",
   //                 chaotic = "focused, serious",
   //                 somber = "quiet, reflective"
   //             },
   //             moodModifiers = new MoodModifier
   //             {
   //                 neutral = "maintain a composed, articulate, and analytical tone.",
   //                 anxious = "speak with sharpened precision, as if calculating risks under pressure.",
   //                 chaotic = "maintain composure but let subtle irritation or disbelief slip through.",
   //                 somber = "adopt a quiet, dignified tone with restrained empathy.",
   //                 confident = "speak with poised authority, as though your predictions are unfolding exactly as expected."
   //             }
   //         };

   //         StorytellerPersonas = StorytellerPersonas ?? new Dictionary<string, StorytellerPersonaDef>();
   //         if (!StorytellerPersonas.ContainsKey("Cassandra"))
   //         {  
   //             StorytellerPersonas.Add("Cassandra", CassandraPersona);
   //             Storytellers.Add("Cassandra"); 
   //         }
   //         if (!StorytellerPersonas.ContainsKey("Fallback"))
   //         {
   //             StorytellerPersonas.Add("Fallback", FallbackPersona);
   //             Storytellers.Add("Fallback");
   //         }
   //         return StorytellerPersonas;
   //     }
    }
}
