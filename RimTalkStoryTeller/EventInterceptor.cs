using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace LivingStoryteller
{
    public static class Patch_IncidentWorker
    {
        public static void Postfix(
            IncidentWorker __instance,
            IncidentParms parms,
            bool __result)
        {
            if (!__result) return;

            var incident = __instance.def;
            var storyteller = Find.Storyteller?.def;
            string defName = storyteller?.defName ?? "";

            // Make the event name human-readable
            string eventName = incident.label;
            if (eventName.NullOrEmpty())
            {
                eventName = incident.defName;
            }
            eventName = FormatEventName(eventName);

            var personaDef = StorytellerPersonaDatabase.GetPersonaDef(defName);
            string persona = personaDef.personaText;
            string voiceId = personaDef.voiceId;

            string colonyContext = "";
            var map = Find.CurrentMap;
            if (map != null)
            {
                int colonists = map.mapPawns.FreeColonistsCount;
                float wealth = map.wealthWatcher.WealthTotal;
                int day = GenDate.DaysPassed;
                colonyContext =
                    $"Colony:{colonists} colonists," + 
                    $"\nWealth:{wealth.ToString("F0")} wealth," +
                    $"\nday:{day}";
            }

            StorytellerAIService.RequestNarration( eventName, incident.category.ToString(), persona, colonyContext, storyteller?.label ?? "Storyteller", defName);
        }

        private static string FormatEventName(string name)
        {
            // Split camelCase/PascalCase into words
            // "AllyAssistance" -> "Ally Assistance"
            // "enemy raid" stays as "enemy raid"
            string spaced = Regex.Replace(name,
                "(?<=[a-z])(?=[A-Z])", " ");
            spaced = Regex.Replace(spaced,
                "(?<=[A-Z])(?=[A-Z][a-z])", " ");

            // Capitalize first letter
            if (spaced.Length > 0)
            {
                spaced = char.ToUpper(spaced[0]) + spaced.Substring(1);
            }

            return spaced;
        }
    }
}
