using Verse;

namespace LivingStoryteller
{
    public class StorytellerPersonaDef : Def
    {
        public string storytellerDefName;   // e.g. "Cassandra"
        public string personaText;          // Persona prompt
        public string voiceId;              // TTS voice identifier
        public string accent;               // Optional accent or style for TTS
        public List<VoiceProvider> voiceProviders = new List<VoiceProvider>();  // RimWorld will auto-fill this list
        public EmotionModifier emotionModifiers = new EmotionModifier(); // Optional modifiers for emotional tone in TTS
        public MoodModifier moodModifiers = new MoodModifier(); // Optional modifiers for mood in TTS
        internal string? GetMood(string mood)
        {
            switch(mood.ToLower())
            {
                default:
                    return this.moodModifiers.neutral;
                case "anxious":
                    return this.moodModifiers.anxious;
                case "chaotic":
                    return this.moodModifiers.chaotic;
                case "somber":
                    return this.moodModifiers.somber;
                case "confident":
                    return this.moodModifiers.confident;

            }
        }

        internal string? GetEmotion(string emotion)
        {
            switch(emotion.ToLower())
            {
                default:
                    return this.emotionModifiers.neutral;
                case "tense":
                    return this.emotionModifiers.tense;
                case "chaotic":
                    return this.emotionModifiers.chaotic;
                case "somber":
                    return this.emotionModifiers.somber;
            }
        }
    }

    public class MoodModifier
    {
        public string neutral;  
        public string anxious;
        public string chaotic;
        public string somber;
        public string confident;
    }

    public class EmotionModifier
    {
        public string neutral; 
        public string tense; 
        public string chaotic;
        public string somber;
    }
    public class VoiceProvider
    {
        public string name;   // attribute: name="google"
        public string voice;  // element: <voice>Zephyr</voice>
    }
}
