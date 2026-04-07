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

        public static string GetPersonaFor(string storytellerDefName)
        {
            LogManager.Log($"Retrieving persona for storyteller: {storytellerDefName}");
            var match = DefDatabase<StorytellerPersonaDef>
                .AllDefs
                .FirstOrDefault(d => d.storytellerDefName == storytellerDefName);

            return match?.personaText ?? fallback?.personaText ?? "";
        }
    }
}