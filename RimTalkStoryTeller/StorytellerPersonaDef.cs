using Verse;

namespace LivingStoryteller
{
    public class StorytellerPersonaDef : Def
    {
        public string storytellerDefName;   // e.g. "Cassandra"
        public string personaText;          // Persona prompt
        public string voiceId;              // TTS voice identifier
    }
}
