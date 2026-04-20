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

    public class EmotionModifier
    {
        public string neutral;  // e.g. "Happy", "Sad", "Angry"
        public string tense; // e.g. "Increase pitch", "Add reverb"
        public string chaotic;
        public string somber;
    }
    public class VoiceProvider
    {
        public string name;   // attribute: name="google"
        public string voice;  // element: <voice>Zephyr</voice>
    }
}
