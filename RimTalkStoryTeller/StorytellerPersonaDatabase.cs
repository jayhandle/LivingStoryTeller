using System.Linq;
using Verse;

namespace LivingStoryteller
{
    [StaticConstructorOnStartup]
    public static class StorytellerPersonaDatabase
    {
        private static readonly StorytellerPersonaDef fallback;

        static StorytellerPersonaDatabase()
        {
            fallback = DefDatabase<StorytellerPersonaDef>
                .AllDefs
                .FirstOrDefault(d => d.storytellerDefName == "Fallback");
        }

        public static StorytellerPersonaDef GetPersonaDef(string storytellerDefName)
        {
            return DefDatabase<StorytellerPersonaDef>
                .AllDefs
                .FirstOrDefault(d => d.storytellerDefName == storytellerDefName)
                ?? fallback;
        }

        public static string GetPersonaText(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.personaText ?? "";
        }

        public static string GetVoiceId(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.voiceId ?? "default_voice";
        }
    }
}