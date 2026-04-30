using Verse;

namespace LivingStoryteller
{
    public class StorytellerPersonaDef : IExposable
    {
        public string storytellerDefName;   // e.g. "Cassandra"
        public string personaText;          // Persona prompt
        public string voiceId;              // TTS voice identifier
        public string accent;               // Optional accent or style for TTS
        public List<VoiceProvider> voiceProviders = new List<VoiceProvider>();  // RimWorld will auto-fill this list
        public EmotionModifier emotionModifiers = new EmotionModifier(); // Optional modifiers for emotional tone in TTS
        public MoodModifier moodModifiers = new MoodModifier(); // Optional modifiers for mood in TTS
        internal string? gender;

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

        public void ExposeData()
        {
            Scribe_Values.Look(ref storytellerDefName, "storytellerDefName");
            Scribe_Values.Look(ref personaText, "personaText");
            Scribe_Values.Look(ref voiceId, "voiceId");
            Scribe_Values.Look(ref accent, "accent");
            Scribe_Collections.Look(ref voiceProviders, "voiceProviders", LookMode.Deep);
            Scribe_Deep.Look(ref emotionModifiers, "emotionModifiers");
            Scribe_Deep.Look(ref moodModifiers, "moodModifiers");
            LogManager.Log($"[LivingStoryteller] Loaded StorytellerPersonaDef: {storytellerDefName} | VoiceId: {voiceId} | Accent: {accent} | PersonaText length: {personaText?.Length ?? 0}");
        }
    }

    public class MoodModifier : IExposable
    {
        public string neutral;  
        public string anxious;
        public string chaotic;
        public string somber;
        public string confident;

        public void ExposeData()
        {
            Scribe_Values.Look(ref neutral, "neutral");
            Scribe_Values.Look(ref anxious, "anxious");
            Scribe_Values.Look(ref chaotic, "chaotic");
            Scribe_Values.Look(ref somber, "somber");
            Scribe_Values.Look(ref confident, "confident");
        }
    }

    public class EmotionModifier : IExposable
    {
        public string neutral; 
        public string tense; 
        public string chaotic;
        public string somber;

        public void ExposeData()
        {
            Scribe_Values.Look(ref neutral, "neutral");
            Scribe_Values.Look(ref tense, "tense");
            Scribe_Values.Look(ref chaotic, "chaotic");
            Scribe_Values.Look(ref somber, "somber");
        }
    }

    public class VoiceProvider : IExposable
    {
        public string name;   // attribute: name="google"
        public string voice;  // element: <voice>Zephyr</voice>

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref voice, "voice");
        }
    }
}
