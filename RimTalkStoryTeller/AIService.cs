using Extension.LivingStoryTeller;
using Google.GenAI.Types;
using LivingStoryTeller;
using RimWorld;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{
    [StaticConstructorOnStartup]
    public static class StorytellerAIService
    {
        private class NarrationRequest
        {
            public string IncidentLabel;
            public string IncidentCategory;
            public string Persona;
            public string ColonyContext;
            public string StorytellerName;
            public string PersonaDefName;
            public string EventKey;
        }

        private static bool isWaiting = false;
        private static float lastNarrationTime = -999f;
        private static List<string> eventProcessing = new List<string>();
        private static Queue<NarrationRequest> queuedNarrationRequests = new Queue<NarrationRequest>();
        private static readonly object queuedNarrationLock = new object();
        // Thread-safe narration queue
        private static readonly object pendingLock = new object();
        private static readonly object eventProcessingLock = new object();

        private static string pendingName;
        private static string pendingText;
        private static Texture2D pendingPortrait;
        private static bool hasPending = false;

        // Thread-safe log queue
        private static readonly object logLock = new object();
        private static List<string> pendingLog = new List<string>();

        public static void ProcessPending()
        {
            // Process pending logs
            lock (logLock)
            {
                if (pendingLog.Any())
                {
                    while (pendingLog.Count() > 1)
                    {
                        var kvp = pendingLog[0];
                        var split = kvp.Split('|');
                        string msg = split[0];
                        string level = split[1];
                        if (level == "error")
                            LogManager.Error(msg);
                        else if (level == "warning")
                            LogManager.Warning(msg);
                        else
                            LogManager.Log(msg);
                        pendingLog.RemoveAt(0);
                    }
                }
            }

            TryReleaseQueuedNarration();
            

            if(ModOptions.Settings.TTSEnabled && TTSService.ProcessingAudio)
            {
                return;
            }
            // Process pending narration
            lock (pendingLock)
            {
                if (!hasPending) return;
                LogManager.Log("Pending narration found.");
                // If RPG Dialog is active, wait for any existing
                // event dialog to close before showing ours
                if (RPGDialogBridge.IsAvailable)
                {
                    var existingDialog = Find.WindowStack?
                        .WindowOfType<Dialog_NodeTree>();
                    if (existingDialog != null)
                    {
                        // Keep pending, check again next frame
                        return;
                    }
                }

                hasPending = false;

                string name = pendingName;
                string text = pendingText;
                Texture2D portrait = pendingPortrait;

                pendingName = null;
                pendingText = null;
                pendingPortrait = null;

                if (text.NullOrEmpty()) return;

                if(ModOptions.Settings.TTSEnabled)
                {
                    LogManager.Log("Processing pending audio for narration.");
                    TTSService.ProcessPendingAudio();
                }

                if (RPGDialogBridge.IsAvailable)
                {
                    RPGDialogBridge.ShowNarration(name, text);
                }
                else
                {
                    var existing = Find.WindowStack?
                        .WindowOfType<StorytellerWindow>();
                    if (existing != null)
                    {
                        existing.Close(false);
                    }

                    if (Find.WindowStack != null)
                    {
                        Find.WindowStack.Add(
                            new StorytellerWindow(
                                name, text, portrait));
                    }
                }
            }

            //Process mood over time
            DecayMood();
        }

        private static void DecayMood()
        {
            LogManager.Log($"Decaying mood. Before decay - Stress: {ModOptions.Settings.Stress}, Chaos: {ModOptions.Settings.Chaos}, Sympathy: {ModOptions.Settings.Sympathy}, Confidence: {ModOptions.Settings.Confidence}");
            float decay = 0.001f; // slow decay per frame

            ModOptions.Settings.Stress = Mathf.Max(0f, ModOptions.Settings.Stress - decay);
            ModOptions.Settings.Chaos = Mathf.Max(0f, ModOptions.Settings.Chaos - decay);
            ModOptions.Settings.Sympathy = Mathf.Max(0f, ModOptions.Settings.Sympathy - decay);
            ModOptions.Settings.Confidence = Mathf.Max(0f, ModOptions.Settings.Confidence - decay);
        }

        private static void QueueLog(string message, string level = "message")
        {
            lock (logLock)
            {
                pendingLog.Add($"{message}|{level}");
            }
        }

        public static void RequestNarration(string incidentLabel, string incidentCategory, string persona, string colonyContext, string storytellerName, string PersonaDefName)
        {
            var eventKey = incidentLabel + "|" + incidentCategory;
            var request = new NarrationRequest
            {
                IncidentLabel = incidentLabel,
                IncidentCategory = incidentCategory,
                Persona = persona,
                ColonyContext = colonyContext,
                StorytellerName = storytellerName,
                PersonaDefName = PersonaDefName,
                EventKey = eventKey
            };

            LogManager.Log("Requesting narration for event: " + incidentLabel + " (Category: " + incidentCategory + ") EventKey:" + eventKey);

            lock (eventProcessingLock)
            {
                if (eventProcessing.Contains(eventKey))
                {
                    LogManager.Log(eventKey + " is already processing a narration for this event. Skipping duplicate request.");
                    return;
                }

                eventProcessing.Add(eventKey);
            }

            LogManager.Log("[LivingStoryteller] Event intercepted: " + eventKey);
            var settings = ModOptions.Settings;

            if (settings.ApiKey.NullOrEmpty())
            {
                LogManager.Warning( "[LivingStoryteller] No API key configured. " + "Go to Mod Settings > The Living Storyteller.");
                RemoveEventProcessing(eventKey);
                return;
            }

            if (IsCooldownActive())
            {
                if (settings.SkipEventsDuringCooldown)
                {
                    LogManager.Log("Cooldown active and skip enabled. Skipping narration for event: " + incidentLabel + " (Category: " + incidentCategory + ")");
                    RemoveEventProcessing(eventKey);
                    return;
                }

                EnqueueNarrationRequest(request, "cooldown active");
                return;
            }

            if (isWaiting)
            {
                EnqueueNarrationRequest(request, "already waiting for narration response");
                return;
            }

            StartNarrationRequest(request);
        }

        private static bool IsCooldownActive()
        {
            return Time.time - lastNarrationTime < ModOptions.Settings.cooldownSeconds;
        }

        private static void EnqueueNarrationRequest(NarrationRequest request, string reason)
        {
            lock (queuedNarrationLock)
            {
                queuedNarrationRequests.Enqueue(request);
                LogManager.Log("Narration delayed (" + reason + "). Queued event: " + request.EventKey + ". Queue size: " + queuedNarrationRequests.Count);
            }
        }

        private static void TryReleaseQueuedNarration()
        {
            if (isWaiting || IsCooldownActive())
                return;

            NarrationRequest next = null;
            lock (queuedNarrationLock)
            {
                if (queuedNarrationRequests.Count > 0)
                {
                    next = queuedNarrationRequests.Dequeue();
                    LogManager.Log("Cooldown complete. Releasing queued event: " + next.EventKey + ". Remaining queue size: " + queuedNarrationRequests.Count);
                }
            }

            if (next == null)
                return;

            lock (eventProcessingLock)
            {
                if (!eventProcessing.Contains(next.EventKey))
                {
                    // Event can be removed externally; do not process stale queued requests.
                    return;
                }
            }

            StartNarrationRequest(next);
        }

        private static void RemoveEventProcessing(string eventKey)
        {
            lock (eventProcessingLock)
            {
                eventProcessing.Remove(eventKey);
            }
        }

        private static void StartNarrationRequest(NarrationRequest request)
        {
            lastNarrationTime = Time.time;
            isWaiting = true;

            // Cache portrait on MAIN THREAD
            Texture2D portrait = GetStorytellerPortrait();



            string systemPrompt = request.Persona + settings.PersonaText;
            string userMessage = $"Event: {request.IncidentLabel}";
            userMessage += (request.ColonyContext.NullOrEmpty() ? "" : request.ColonyContext);
            string emotion = string.Empty;
            string mood = string.Empty;
            LogManager.Log($"Use Emotion: {settings.UseEmotion}");
            if (settings.UseEmotion)
            {
                emotion = GetEmotion(request.IncidentCategory, request.IncidentLabel, request.PersonaDefName);
                LogManager.Log($"emotion: {emotion}");
                mood = GetMoodDescriptor(request.PersonaDefName);
                LogManager.Log($"mood: {mood}");
                UpdateMood(request.IncidentCategory, request.IncidentLabel);

                systemPrompt += $"\nUse a {emotion} emotional tone.";
                systemPrompt += $"\nYour current mood is {mood}";

                systemPrompt += "\nAdjust your narration style to reflect both the immediate emotion and the long-term mood.";

                userMessage += $"\nEmotional tone: {emotion}";
                userMessage += $"\nmood: {mood}";

            }
            LogManager.Log($"Use accent:{settings.UseAccent}." );
            if (settings.UseAccent)
            {
                var accent = StorytellerPersonaDatabase.GetAccent(request.PersonaDefName);
                LogManager.Log($"accent: {accent}");
                systemPrompt += $"\nUse a {accent} accent.";
                userMessage += $"\nAccent: {accent}";
            }


            systemPrompt += "\nKeep in mind of past events in the Memory, if there are any.";
            userMessage += GetMemories();

            string name = request.StorytellerName;
            string endpoint = settings.Endpoint;
            string apiKey = settings.ApiKey.Trim();
            string model = settings.ModelName;
            string eventKey = request.EventKey;
            string personaDefName = request.PersonaDefName;

            Task.Run(async() =>
            {
                var retryCount = 3;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        QueueLog("Calling AI API for narration (Attempt " + (i + 1) + "/" + retryCount + ")...");
                        string response = CallAPI( endpoint, apiKey, model, systemPrompt, userMessage);

                        if (!response.NullOrEmpty())
                        {
                            QueueLog("Narration received.");
                            if(settings.TTSEnabled) TTSService.RequestSpeech(response, personaDefName, emotion, mood);
                            lock (pendingLock)
                            {
                                pendingName = name;
                                pendingText = response;
                                pendingPortrait = portrait;
                                hasPending = true;
                            }
                            break;
                        }
                        else
                        {
                            QueueLog("[LivingStoryteller] " + "Empty AI response.", "warning");
                        }
                    }
                    catch (Exception ex)
                    {
                        QueueLog( "[LivingStoryteller] AI call failed: " + ex.Message, "error");
                    }
                }

                isWaiting = false;
                RemoveEventProcessing(eventKey);
            });
        }

        private static string GetMemories()
        {
            var memories = string.Empty;
            if (LivingStorytellerTicksComponent.MemoryManager.ShortTerm.Any())
            {
                memories = $"\nRecent Memory: ";
                for (int i = 0; i < LivingStorytellerTicksComponent.MemoryManager.ShortTerm.Count; i++)
                {
                    MemoryRecord? mem = LivingStorytellerTicksComponent.MemoryManager.ShortTerm[i];
                    memories += $"{mem.Type} - {mem.Description} - {mem.AgeTicks/ 60000} days passed";
                }
            }

            if (LivingStorytellerTicksComponent.MemoryManager.LongTerm.Any())
            {
                memories += $"\nLong-Term Memory: ";
                foreach (var mem in LivingStorytellerTicksComponent.MemoryManager.LongTerm)
                {
                    memories += $"{mem.Type} - {mem.Description} - {mem.AgeTicks / 60000} days passed";
                }
            }
            return memories;
        }

        private static void UpdateMood(string category, string label)
        {
            LogManager.Log($"Updating mood based on event. Category: {category}, Label: {label}");
            // Pawn death → sympathy + stress
            if (label.Contains("Died") || category == "PawnDeath")
            {
                ModOptions.Settings.Sympathy += 0.4f;
                ModOptions.Settings.Stress += 0.2f;
            }

            // Big threats → chaos + stress
            if (category == "ThreatBig")
            {
                ModOptions.Settings.Chaos += 0.5f;
                ModOptions.Settings.Stress += 0.3f;
            }

            // Small threats → chaos
            if (category == "ThreatSmall" || category == "MajorThreat")
            {
                ModOptions.Settings.Chaos += 0.3f;
            }

            // Positive events → confidence
            if (label.Contains("Inspired") || label.Contains("Marriage") || label.Contains("Birth"))
            {
                ModOptions.Settings.Confidence += 0.4f;
            }

            // Clamp values
            ModOptions.Settings.Stress = Mathf.Clamp(ModOptions.Settings.Stress, 0f, 5f);
            ModOptions.Settings.Chaos = Mathf.Clamp(ModOptions.Settings.Chaos, 0f, 5f);
            ModOptions.Settings.Sympathy = Mathf.Clamp(ModOptions.Settings.Sympathy, 0f, 5f);
            ModOptions.Settings.Confidence = Mathf.Clamp(ModOptions.Settings.Confidence, 0f, 5f);
        }
        private static string GetEmotion(string incidentCategory, string incidentLabel, string personaDef)
        {
            LogManager.Log($"Determining emotion for incidentCategory: {incidentCategory}, incidentLabel: {incidentLabel}, personaDef: {personaDef}");
            var emotion = "neutral";
            // Deaths
            if (incidentLabel.Contains("Died") || incidentCategory == "PawnDeath") emotion = "somber";

            // Big threats
            if (incidentCategory == "ThreatBig") emotion = "tense";

            // Randy chaos
            if (incidentCategory == "ThreatSmall" || incidentCategory == "MajorThreat") emotion = "chaotic";

            return StorytellerPersonaDatabase.GetEmotionalTone(personaDef, emotion);
        }

        private static string GetMoodDescriptor(string personaDef)
        {
            LogManager.Log($"Determining mood descriptor for personaDef: {personaDef}. Current mood values - Stress: {ModOptions.Settings.Stress}, Chaos: {ModOptions.Settings.Chaos}, Sympathy: {ModOptions.Settings.Sympathy}, Confidence: {ModOptions.Settings.Confidence}");
            var mood = "neutral";
            if (ModOptions.Settings.Stress > 3f) mood = "anxious";
            if (ModOptions.Settings.Chaos > 3f) mood = "chaotic";
            if (ModOptions.Settings.Sympathy > 3f) mood = "somber";
            if (ModOptions.Settings.Confidence > 3f) mood = "confident";

            return StorytellerPersonaDatabase.GetMood(personaDef, mood);
        }

        private static Texture2D GetStorytellerPortrait()
        {
            var def = Find.Storyteller?.def;
            if (def == null) return null;

            if (def.portraitTinyTex != null)
                return def.portraitTinyTex;

            if (def.portraitLargeTex != null)
                return def.portraitLargeTex;

            return null;
        }

        private static string CallAPI(
            string endpoint,
            string apiKey,
            string model,
            string systemPrompt,
            string userMessage)
        {

            var request = AIProviderFactory.JSONRequest(model, systemPrompt, userMessage);
            return AIProviderFactory.GetResponse(request).GetAwaiter().GetResult();
        }

        internal static void GreetPlayer(Map map)
        {
            var mem = LivingStorytellerTicksComponent.MemoryManager;

            string recent = string.Join("; ",
                mem.LongTerm.Select(m => m.Description));

            string arcs = string.Join("; ",
                mem.ActiveArcs.Select(a => a.Name));

            var storyteller = Find.Storyteller?.def;
            string defName = storyteller?.defName ?? "";

            var personaDef = StorytellerPersonaDatabase.GetPersonaDef(defName);
            string persona = personaDef.storytellerDefName;

            string prompt = $@"You are {persona}, the Living Storyteller. The player has just loaded back into their RimWorld colony. Give them an in-character greeting. Summarize recent events/memories. Comment on the colony's current situation. Suggest what they might want to do next, if you can think of anything. Do not break character.";
            string colonyContext = "";
            if (map != null)
            {
                int colonists = map.mapPawns.FreeColonistsCount;
                float wealth = map.wealthWatcher.WealthTotal;
                int day = GenDate.DaysPassed;
                colonyContext =
                    $"\nColony: {colonists} colonists" +
                    $"\nWealth: {wealth.ToString("F0")} wealth" +
                    $"\nday: {day}";
            }

            RequestNarration("Welcome Back","Greeting", prompt, colonyContext, personaDef.storytellerDefName, defName);
        }
    
    }
}
