using RimWorld;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace LivingStoryteller
{
    internal static class EchoTalesIntegration
    {
        private const string EchoTalesPackageId = "gerik.echotales";
        private const int CheckIntervalTicks = 600;
        private const int tickOffset = 72000; // wait 2 in game hours or 2 minutes
        private static int nextCheckTick;
        private static bool loggedIntegrationDisabled;
        private static bool loggedModInactive;
        private static bool loggedComponentSchema;
        private static bool? _isAvailable;
        private static object storyMemory;

        public static bool IsAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    _isAvailable =
                        ModLister.GetActiveModWithIdentifier(
                            EchoTalesPackageId) != null;

                    if (_isAvailable.Value)
                    {
                        LogManager.Log("EchoTales Detected");
                        int ticksGame = Find.TickManager.TicksGame;
                        int currentDay = GenDate.DaysPassed;
                        LogManager.Log("[EchoTales] Probe start. day=" + currentDay +
                            ", ticks=" + ticksGame +
                            ", readEveryNewEntry=" + ModOptions.Settings.EchoTalesReadEveryNewEntry +
                            ", lastCommentDay=" + LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay +
                            ", lastTaleSignature=" + LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature );

                    }
                }
                return _isAvailable.Value;
            }
        }

        public static void TryProcessDailyTale()
        {
            if (!ModOptions.Settings.EnableEchoTalesIntegration)
            {
                if (!loggedIntegrationDisabled)
                {
                    LogManager.Log("[EchoTales] Integration disabled in mod settings.");
                    loggedIntegrationDisabled = true;
                }
                return;
            }

            if (loggedIntegrationDisabled)
            {
                LogManager.Log("[EchoTales] Integration enabled in mod settings.");
                loggedIntegrationDisabled = false;
            }

            if (Current.Game == null || Find.TickManager == null)
                return;

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextCheckTick)
                return;

            nextCheckTick = ticksGame + CheckIntervalTicks;

            int currentDay = GenDate.DaysPassed;
            bool readEveryNewEntry = ModOptions.Settings.EchoTalesReadEveryNewEntry;

            if (!readEveryNewEntry)
            {
                if (currentDay <= LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay)
                {
                    //LogManager.Log("[EchoTales] Skip: already commented today or before. currentDay=" + currentDay +
                    //    ", lastCommentDay=" + LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay);
                    return;
                }
            }
           
            if (!TryGetLatestTale(out var taleText, out var taleDay))
            {
                //LogManager.Log("[EchoTales] Skip: probe could not retrieve a latest tale entry.");
                return;
            }

            if (taleText.NullOrEmpty())
            {
                LogManager.Log("[EchoTales] Skip: latest tale text is empty.");
                return;
            }

            if(taleDay.HasValue && taleDay.Value <= LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature)
            {
                //LogManager.Log("[EchoTales] Skip: latest tale signature matches or is less than last processed signature. taleDay=" + taleDay.Value +
                //    ", lastTaleSignature=" + LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature);
                return;
            }

            //if (taleDay.HasValue && (taleDay.Value + tickOffset) <= ticksGame)
            //{
            //    LogManager.Log("[EchoTales] Skip: latest tale is from a previous day. taleDay=" + taleDay.Value + ", ticksGame=" + ticksGame);
            //    return;
            //}

            string eventName = "{" +
                "\"event\":\"EchoTalesDailyEntry\"," +
                "\"category\":\"EchoTales\"," +
                "\"day\":\"" + currentDay + "\"," +
                "\"entry\":\"" + EscapeJson(taleText) + "\"," +
                "\"prompt\":\"Summarize the day's entry and make a comment about it.\"" +
                "}";

            LogManager.Log("[EchoTales] Daily tale accepted. day=" + currentDay +
                ", taleDay=" + (taleDay?.ToString()) +
                ", taleLength=" + taleText.Length +
                ". Requesting storyteller commentary.");
            RequestNarration.Request(eventName, "EchoTales");

            LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay = currentDay;
            LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature = taleDay.HasValue ? taleDay.Value : -1;
            LogManager.Log($"[EchoTales] State updated. lastCommentDay:{LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesCommentDay}: lastSig:{LivingStoryTeller.LivingStorytellerTicksComponent.LastEchoTalesTaleSignature}:");
        }

        private static bool TryGetLatestTale(out string taleText, out int? taleDay)
        {
            taleText = string.Empty;
            taleDay = null;

            try
            {
                if (storyMemory == null)
                {
                    var echoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => string.Equals(a.GetName().Name, "EchoTales", StringComparison.OrdinalIgnoreCase));

                    if (echoAssembly == null)
                    {
                        LogManager.Warning("[EchoTales] Probe detail: EchoTales assembly not loaded.");
                        _isAvailable = false;
                        return false;
                    }

                    storyMemory = GetEchoStoryMemory(echoAssembly);
                }
                if (storyMemory == null)
                {
                   LogManager.Log("[EchoTales] storyMemory not found.");
                   _isAvailable = false;
                    return false;
                   // storyMemory = gameComponent;
                }
               // LogManager.Log("[EchoTales] storyMemory found: " + storyMemory.SavedEntries.Count());
                var entries = GetMemberValue(storyMemory, "SavedEntries", "savedEntries", "Entries", "entries");
                if (entries == null)
                {
                    //LogManager.Log("[EchoTales] Probe detail: no entries found in EchoTales story memory.");
                    return false;
                }
                var entry = GetLastFromEnumerable(entries);
                
                if (entry == null)
                {
                   // LogManager.Log("[EchoTales] Probe detail: no latest entry found in EchoTales component entries collection.");
                    return false;
                }

                //LogManager.Log($"[EchoTales] Probe detail: latest entry found. Tick:{entry.GetType()}: entryText: " + ReadEntryText(entry));

                taleDay = GetMemberValue(entry, "Ticks", "ticks") is int t ? t : (int?)null;
                taleText = GetMemberValue(entry, "Text", "text", "Story", "story", "Content", "content") as string;
                return !string.IsNullOrWhiteSpace(taleText); 
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

        private static object GetEchoStoryMemory(Assembly echoAssembly)
        {
            var memoryType = echoAssembly.GetType("EchoTales.EchoStoryMemory", false, true);
            if (memoryType == null)
                return null;

            var instanceProperty = memoryType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (instanceProperty == null)
                return null;

            try
            {
                return instanceProperty.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        private static object GetLatestEntryFromComponent(object component)
        {
            var entriesContainer = GetMemberValue(component,
                "entries", "Entries", "SavedEntries", "savedEntries", "storyEntries", "StoryEntries", "stories", "Stories",
                "chronicle", "Chronicle", "memory", "Memory", "PersistentMemory", "persistentMemory",
                "echoStoryMemory", "EchoStoryMemory", "memoryResponse", "newMemory", "story", "Story");

            if (entriesContainer != null)
            {
                var knownEntry = GetLastFromEnumerable(entriesContainer);
                if (knownEntry != null)
                    return knownEntry;

                var nestedEntriesContainer = GetMemberValue(entriesContainer,
                    "entries", "Entries", "SavedEntries", "savedEntries", "storyEntries", "StoryEntries", "stories", "Stories",
                    "chronicle", "Chronicle", "memory", "Memory", "PersistentMemory", "persistentMemory",
                    "echoStoryMemory", "EchoStoryMemory");

                if (nestedEntriesContainer != null)
                {
                    var nestedKnownEntry = GetLastFromEnumerable(nestedEntriesContainer);
                    if (nestedKnownEntry != null)
                        return nestedKnownEntry;

                    LogManager.Log("[EchoTales] Probe detail: nested entries container found but it was empty or non-enumerable. containerType=" + nestedEntriesContainer.GetType().FullName);
                }

                if (LooksLikeEchoTalesEntry(entriesContainer))
                    return entriesContainer;

                LogManager.Log("[EchoTales] Probe detail: known entries container found, but it was empty or non-enumerable. containerType=" + entriesContainer.GetType().FullName);
            }
            else if (!loggedComponentSchema)
            {
                LogManager.Log("[EchoTales] Probe detail: known entry member names were not found. Dumping component schema for diagnosis.");
                LogComponentSchema(component);
                loggedComponentSchema = true;
            }

            var fallbackEntry = TryFindLatestEntryByHeuristic(component, out var sourcePath);
            if (fallbackEntry != null)
            {
                LogManager.Log("[EchoTales] Probe detail: heuristic entry discovery succeeded via " + sourcePath + ".");
                return fallbackEntry;
            }

            var broadEntry = TryFindAnyEnumerableEntryFromComponent(component, out var broadSourcePath);
            if (broadEntry != null)
            {
                LogManager.Log("[EchoTales] Probe detail: broad enumerable discovery succeeded via " + broadSourcePath + ".");
                return broadEntry;
            }

            LogManager.Log("[EchoTales] Probe detail: no enumerable entry candidate could be extracted from the component.");

            return null;
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

        private static object TryFindLatestEntryByHeuristic(object root, out string sourcePath)
        {
            sourcePath = string.Empty;

            if (root == null)
                return null;

            var visited = new HashSet<int>();
            var queue = new Queue<(object node, int depth, string path)>();
            queue.Enqueue((root, 0, "component"));

            while (queue.Count > 0)
            {
                var (node, depth, path) = queue.Dequeue();
                if (node == null)
                    continue;

                int id = RuntimeHelpers.GetHashCode(node);
                if (!visited.Add(id))
                    continue;

                if (depth > 2)
                    continue;

                foreach (var member in GetReadableMembers(node.GetType()))
                {
                    if (!TryReadMember(node, member, out var value) || value == null)
                        continue;

                    string memberPath = path + "." + member.Name;

                    if (value is IEnumerable enumerable && value is not string)
                    {
                        var last = GetLastFromEnumerable(enumerable);
                        if (last != null && LooksLikeEchoTalesEntry(last))
                        {
                            sourcePath = memberPath;
                            return last;
                        }

                        if (depth < 2)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item == null || IsSimpleType(item.GetType()))
                                    continue;

                                queue.Enqueue((item, depth + 1, memberPath + "[]"));
                                break;
                            }
                        }
                    }
                    else if (!IsSimpleType(value.GetType()) && depth < 2)
                    {
                        queue.Enqueue((value, depth + 1, memberPath));
                    }
                }
            }

            return null;
        }

        private static object TryFindAnyEnumerableEntryFromComponent(object root, out string sourcePath)
        {
            sourcePath = string.Empty;

            if (root == null)
                return null;

            var visited = new HashSet<int>();
            var queue = new Queue<(object node, int depth, string path)>();
            queue.Enqueue((root, 0, "component"));

            while (queue.Count > 0)
            {
                var (node, depth, path) = queue.Dequeue();
                if (node == null)
                    continue;

                int id = RuntimeHelpers.GetHashCode(node);
                if (!visited.Add(id))
                    continue;

                if (depth > 4)
                    continue;

                foreach (var member in GetReadableMembers(node.GetType()))
                {
                    if (!TryReadMember(node, member, out var value) || value == null)
                        continue;

                    string memberPath = path + "." + member.Name;

                    if (value is IEnumerable enumerable && value is not string)
                    {
                        string summary = DescribeEnumerable(enumerable);
                        LogManager.Log("[EchoTales] Probe detail: enumerable member " + memberPath + " => " + summary);

                        var last = GetLastFromEnumerable(enumerable);
                        if (last != null)
                        {
                            if (last is string || !IsSimpleType(last.GetType()))
                            {
                                sourcePath = memberPath;
                                return last;
                            }
                        }

                        if (depth < 4)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item == null || item is string || IsSimpleType(item.GetType()))
                                    continue;

                                queue.Enqueue((item, depth + 1, memberPath + "[]"));
                                break;
                            }
                        }
                    }
                    else if (!IsSimpleType(value.GetType()) && depth < 4)
                    {
                        queue.Enqueue((value, depth + 1, memberPath));
                    }
                }
            }

            return null;
        }

        private static bool LooksLikeEchoTalesEntry(object candidate)
        {
            if (candidate == null)
                return false;

            var text = ReadEntryText(candidate);
            if (!text.NullOrEmpty())
                return true;

            return HasMember(candidate.GetType(), "day", "Day", "story", "Story", "text", "Text", "content", "Content", "entryText", "EntryText", "description", "Description", "summary", "Summary");
        }

        private static void LogComponentSchema(object component)
        {
            if (component == null)
                return;

            try
            {
                var type = component.GetType();
                LogManager.Log("[EchoTales] Component type: " + type.FullName);

                foreach (var member in GetReadableMembers(type))
                {
                    string details = "unknown";
                    if (TryReadMember(component, member, out var value))
                    {
                        if (value == null)
                        {
                            details = "null";
                        }
                        else if (value is string str)
                        {
                            details = "string(len=" + str.Length + ")";
                        }
                        else if (value is IEnumerable)
                        {
                            details = DescribeEnumerable((IEnumerable)value);
                        }
                        else
                        {
                            details = value.GetType().FullName;
                        }
                    }

                    LogManager.Log("[EchoTales] Component member: " + member.Name + " => " + details);
                }
            }
            catch (Exception ex)
            {
                LogManager.Warning("[EchoTales] Failed to log component schema: " + ex.Message);
            }
        }

        private static string DescribeEnumerable(IEnumerable enumerable)
        {
            if (enumerable == null)
                return "enumerable(null)";

            int count = -1;
            if (enumerable is ICollection collection)
                count = collection.Count;

            object last = GetLastFromEnumerable(enumerable);
            string lastType = last == null ? "null" : last.GetType().Name;

            if (count >= 0)
                return "enumerable(" + enumerable.GetType().Name + ", count=" + count + ", lastType=" + lastType + ")";

            return "enumerable(" + enumerable.GetType().Name + ", lastType=" + lastType + ")";
        }

        private static IEnumerable<MemberInfo> GetReadableMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length == 0)
                    yield return prop;
            }

            foreach (var field in type.GetFields(flags))
                yield return field;
        }

        private static bool TryReadMember(object target, MemberInfo member, out object value)
        {
            value = null;

            try
            {
                switch (member)
                {
                    case PropertyInfo prop:
                        value = prop.GetValue(target);
                        return true;
                    case FieldInfo field:
                        value = field.GetValue(target);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool HasMember(Type type, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in memberNames)
            {
                if (type.GetProperty(name, flags) != null || type.GetField(name, flags) != null)
                    return true;
            }

            return false;
        }

        private static bool IsSimpleType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
                return true;

            return type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid);
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