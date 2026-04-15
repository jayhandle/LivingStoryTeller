using Verse;

namespace LivingStoryteller
{
    public class StorytellerPersonaDef : Def
    {
        public string storytellerDefName;   // e.g. "Cassandra"
        public string personaText;          // Persona prompt
        public string voiceId;              // TTS voice identifier
        public List<VoiceProvider> voiceProviders = new List<VoiceProvider>();  // RimWorld will auto-fill this list
    }

    public class VoiceProvider
    {
        public string name;   // attribute: name="google"
        public string voice;  // element: <voice>Zephyr</voice>
    }
}
