using RimWorld;
using System.Collections;
using System.Reflection;
using Verse;

namespace LivingStoryteller
{
    internal static class EchoTalesIntegration
    {
        private const string EchoTalesPackageId = "gerik.echotales";
        private const int CheckIntervalTicks = 600;

        private static int nextCheckTick;

        public static void TryProcessDailyTale()
        {
            if (!ModOptions.Settings.EnableEchoTalesIntegration)
                return;

            if (Current.Game == null || Find.TickManager == null)
                return;

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextCheckTick)
                return;

            nextCheckTick = ticksGame + CheckIntervalTicks;

            if (!ModsConfig.IsActive(EchoTalesPackageId))
                return;

            int currentDay = GenDate.DaysPassed;
            bool readEveryNewEntry = ModOptions.Settings.EchoTalesReadEveryNewEntry;
            if (!readEveryNewEntry &&
                currentDay <= LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay)
                return;

            if (!TryGetLatestTale(out var taleText, out var signature, out var taleDay))
                return;

            if (taleText.NullOrEmpty())
                return;

            if (taleDay.HasValue && taleDay.Value < currentDay)
                return;

            if (!signature.NullOrEmpty() &&
                signature == LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature)
                return;

            if (!readEveryNewEntry &&
                currentDay == LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay)
                return;

            string trimmedTale = taleText.Length > 1200 ? taleText.Substring(0, 1200) : taleText;
            string eventName = "{" +
                "\"event\":\"EchoTalesDailyEntry\"," +
                "\"category\":\"EchoTales\"," +
                "\"day\":\"" + currentDay + "\"," +
                "\"tale\":\"" + EscapeJson(trimmedTale) + "\"," +
                "\"prompt\":\"Read the EchoTales entry for today and comment on it in character.\"" +
                "}";

            LogManager.Log("[EchoTales] Daily tale found for day " + currentDay + ". Requesting storyteller commentary.");
            RequestNarration.Request(eventName, "EchoTales");

            LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay = currentDay;
            LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature = signature ?? string.Empty;
        }

        private static bool TryGetLatestTale(out string taleText, out string signature, out int? taleDay)
        {
            taleText = string.Empty;
            signature = string.Empty;
            taleDay = null;

            try
            {
                var echoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "EchoTales", StringComparison.OrdinalIgnoreCase));

                if (echoAssembly == null)
                    return false;

                var gameComponent = GetEchoTalesGameComponent(echoAssembly);
                if (gameComponent == null)
                    return false;

                var entry = GetLatestEntryFromComponent(gameComponent);
                if (entry == null)
                    return false;

                taleText = ReadEntryText(entry);
                signature = ReadEntrySignature(entry, taleText);
                taleDay = ReadEntryDay(entry);
                return !taleText.NullOrEmpty();
            }
            catch (Exception ex)
            {
                LogManager.Warning("[EchoTales] Integration probe failed: " + ex.Message);
                return false;
            }
        }

        private static object GetEchoTalesGameComponent(Assembly echoAssembly)
        {
            var componentType = echoAssembly.GetTypes()
                .FirstOrDefault(t => t.Name.IndexOf("EchoTalesGameComponent", StringComparison.OrdinalIgnoreCase) >= 0);

            if (componentType == null || Current.Game?.components == null)
                return null;

            return Current.Game.components.FirstOrDefault(c => c != null && componentType.IsInstanceOfType(c));
        }

        private static object GetLatestEntryFromComponent(object component)
        {
            var entriesContainer = GetMemberValue(component,
                "entries", "Entries", "storyEntries", "StoryEntries", "stories", "Stories",
                "chronicle", "Chronicle", "memory", "Memory", "echoStoryMemory", "EchoStoryMemory");

            if (entriesContainer == null)
                return null;

            return GetLastFromEnumerable(entriesContainer);
        }

        private static string ReadEntryText(object entry)
        {
            var text = GetMemberValue(entry,
                "story", "Story", "text", "Text", "content", "Content", "entryText", "EntryText",
                "description", "Description", "summary", "Summary") as string;

            if (!text.NullOrEmpty())
                return text;

            string fallback = entry.ToString();
            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;
        }

        private static int? ReadEntryDay(object entry)
        {
            var dayObj = GetMemberValue(entry,
                "day", "Day", "dayCounter", "DayCounter", "gameDay", "GameDay", "dayOfYear", "DayOfYear");

            if (dayObj == null)
                return null;

            if (dayObj is int dayInt)
                return dayInt;

            if (int.TryParse(dayObj.ToString(), out var parsed))
                return parsed;

            return null;
        }

        private static string ReadEntrySignature(object entry, string text)
        {
            var idObj = GetMemberValue(entry,
                "id", "Id", "entryId", "EntryId", "guid", "Guid", "timestamp", "Timestamp", "createdAt", "CreatedAt");

            if (idObj != null)
                return idObj.ToString() ?? string.Empty;

            return text ?? string.Empty;
        }

        private static object GetMemberValue(object target, params string[] memberNames)
        {
            if (target == null)
                return null;

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var memberName in memberNames)
            {
                var prop = type.GetProperty(memberName, flags);
                if (prop != null)
                {
                    try
                    {
                        return prop.GetValue(target);
                    }
                    catch
                    {
                    }
                }

                var field = type.GetField(memberName, flags);
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(target);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static object GetLastFromEnumerable(object source)
        {
            if (source is string)
                return null;

            if (source is IEnumerable enumerable)
            {
                object last = null;
                foreach (var item in enumerable)
                {
                    if (item != null)
                        last = item;
                }

                return last;
            }

            return null;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}