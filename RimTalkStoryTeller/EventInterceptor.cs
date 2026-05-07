using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using System.Reflection;
using System.Text.RegularExpressions;
using Verse;

namespace LivingStoryteller
{
    public static class Patch_IncidentWorker
    {
        public static void Postfix( IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result) return;
            LogManager.Log($"Incident triggered: defName: {__instance.def.defName}: label:{__instance.def.label}: cat: {__instance.def.category.ToString()} incident: {__instance.ToStringSafe()}");
            var incident = __instance.def;
            // Make the event name human-readable
            string eventName = incident.label;
            if (eventName.NullOrEmpty())
            {
                eventName = incident.defName;
            }
            eventName = FormatEventName(eventName);

            RequestNarration.Request(eventName, incident.category.ToString());

            
            //var personaDef = StorytellerPersonaDatabase.GetPersonaDef(defName);
            //string persona = personaDef.personaText;
            //string voiceId = personaDef.voiceId;

            //string colonyContext = "";
            //var map = Find.CurrentMap;
            //if (map != null)
            //{
            //    int colonists = map.mapPawns.FreeColonistsCount;
            //    float wealth = map.wealthWatcher.WealthTotal;
            //    int day = GenDate.DaysPassed;
            //    colonyContext =
            //        $"Colony:{colonists} colonists," +
            //        $"\nWealth:{wealth.ToString("F0")} wealth," +
            //        $"\nday:{day}";
            //}

            //StorytellerAIService.RequestNarration(eventName, incident.category.ToString(), persona, colonyContext, storyteller?.label ?? "Storyteller", defName);
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

    public static class Patch_TaleRecorder_RecordTale
    {
        public static void Postfix(TaleDef def, object[] args)
        {
            return; // blocking it now until i know what to do with it, too spammy for now

            LogManager.Log($"[Patch_TaleRecorder_RecordTale]Tale recorded: {def.defName} | {def.label} | {def.description}: with args: {string.Join(", ", args.Select(a => a?.ToString() ?? "null"))}");
            //EventManager.Dispatch("TaleRecorded", new
            //{
            //    tale = def.defName,
            //    args
            //});
        }
    }

    public static class Patch_Storyteller_FiringIncident
    {
        public static void Postfix(bool __result, FiringIncident fi)
        {
            if (fi.def.defName == "GiveQuest_Random") return; // Too spammy and not interesting for storytelling
            LogManager.Log($"[Patch_Storyteller_FiringIncident] Storyteller | {fi.def.defName} | {__result} .");

        }
    }

    public static class Patch_GameCondition_Start
    {
        public static void Postfix(GameCondition cond)
        {
            LogManager.Log($"[Patch_GameCondition_Start] Storyteller | {cond.def.defName} .");

        }
    }

    public static class Patch_Pawn_Kill
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (__instance == null) return;
            var pawn = __instance;
            string pawnName = pawn.Name?.ToStringFull ?? "Unknown";
            string cause = dinfo?.Def?.defName ?? "Unknown";
            if (pawnName == "Unknown" ||
              pawn.Faction != Faction.OfPlayer ||
              !pawn.IsColonist
              )
            {
                return;
            }

            LogManager.Log($"[Patch_Pawn_Kill] Pawn killed: {pawnName} | Cause: {cause}");
            RequestNarration.Request(pawnName + " Died", "PawnDeath");
        }

        //private static void RequestNarration(string pawnName)
        //{
        //    var storyteller = Find.Storyteller?.def;
        //    string defName = storyteller?.defName ?? "";

        //    // Make the event name human-readable
        //    string eventName = $"{pawnName} Died";

        //    var personaDef = StorytellerPersonaDatabase.GetPersonaDef(defName);
        //    string persona = personaDef.personaText;
        //    string voiceId = personaDef.voiceId;

        //    string colonyContext = "";
        //    var map = Find.CurrentMap;
        //    if (map != null)
        //    {
        //        int colonists = map.mapPawns.FreeColonistsCount;
        //        float wealth = map.wealthWatcher.WealthTotal;
        //        int day = GenDate.DaysPassed;
        //        colonyContext =
        //            $"Colony:{colonists} colonists," +
        //            $"\nWealth:{wealth.ToString("F0")} wealth," +
        //            $"\nday:{day}";
        //    }

        //    StorytellerAIService.RequestNarration(eventName, "PawnDeath", persona, colonyContext, storyteller?.label ?? "Storyteller", defName);
        //}
    }

    public static class Patch_Pawn_Downed
    {

        public static void Postfix(Pawn_HealthTracker __instance)
        {
            return; // blocking it now until i know what to do with it, too spammy for now
            if (__instance == null) return;
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;
            var tracker = __instance;
            string pawnName = pawn.Name?.ToStringFull ?? "Unknown";
            if (pawnName == "Unknown" ||
               pawn.Faction != Faction.OfPlayer ||
               !pawn.IsColonist
               )
            {
                return;
            }

            if (tracker.State == PawnHealthState.Mobile ||
                tracker.State == PawnHealthState.Down)
            {
                return;
            }

            LogManager.Log($"[Patch_Pawn_Downed] Pawn: {pawnName}| {pawn.Faction.Name} | is colonist:{pawn.IsColonist} | {tracker.State.ToString()}");

            if (tracker.State == PawnHealthState.Dead)
            {
                var storyteller = Find.Storyteller?.def;
                string defName = storyteller?.defName ?? "";

                // Make the event name human-readable
                string eventName = $"{pawnName} Died";

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

                StorytellerAIService.RequestNarration(eventName, "PawnDeath", persona, colonyContext, storyteller?.label ?? "Storyteller", defName);
            }

        }
    }

    public static class Patch_MentalState_Start
    {
        public static void Postfix(Pawn ___pawn, MentalStateDef stateDef)
        {
            return; // blocking it now until i know what to do with mental states, too spammy for now
            if (___pawn == null) return;
            string pawnName = ___pawn.Name?.ToStringFull ?? "Unknown";
            string stateName = stateDef?.defName ?? "Unknown";
            LogManager.Log($"[Patch_MentalState_Start] Pawn: {pawnName} | Mental State started: {stateName}");
        }
    }

    public static class Patch_Hediff_Added 
    { 
        public static void Postfix(Hediff hediff, Pawn ___pawn)
        {
            return; // blocking it now until i know what to do with hediffs, too spammy for now
            if (___pawn == null) return;
            string pawnName = ___pawn.Name?.ToStringFull ?? "Unknown";
            string hediffName = hediff?.def?.defName ?? "Unknown";
            if (pawnName == "Unknown") return;
            LogManager.Log($"[Patch_Hediff_Added] Pawn: {pawnName} | Hediff added: {hediffName}");
        }
    }

    public static class RequestNarration
    {
        public static void Request(string eventName, string category)
        {
            var storyteller = Find.Storyteller?.def;
            string defName = storyteller?.defName ?? "";

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

            StorytellerAIService.RequestNarration(eventName, category, persona, colonyContext, storyteller?.label ?? "Storyteller", defName);
        }
    }
}
