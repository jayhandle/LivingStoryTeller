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

            persona += " Keep the narration concise, ideally under 100 words. " +
                "Use a tone that fits the event. If it's a minor event, " +
                "be brief and lighthearted. If it's a major crisis, be more " +
                "serious and dramatic. Always relate it back to the colony's " +
                "situation when possible. Keep the reading on a third grade level."+
                "when talking to the player, do not refer to the player as 'player'.";
            string colonyContext = "";
            var map = Find.CurrentMap;
            if (map != null)
            {
                int colonists = map.mapPawns.FreeColonistsCount;
                float wealth = map.wealthWatcher.WealthTotal;
                int day = GenDate.DaysPassed;
                colonyContext =
                    "Colony: " + colonists + " colonists, " +
                    wealth.ToString("F0") + " wealth, day " + day + ".";
            }

            StorytellerAIService.RequestNarration( eventName, incident.category.ToString(), persona, colonyContext, storyteller?.label ?? "Storyteller", voiceId);

            LogManager.Log(
                "[LivingStoryteller] Event intercepted: " + eventName);
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
