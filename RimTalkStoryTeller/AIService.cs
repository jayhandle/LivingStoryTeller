using Extension.LivingStoryTeller;
using Google.GenAI.Types;
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
        private static float moodStress = 0f;        // rises with disasters, starvation, injuries
        private static float moodChaos = 0f;         // rises with raids, threats, explosions
        private static float moodSympathy = 0f;      // rises with pawn deaths, mental breaks
        private static float moodConfidence = 0f;    // rises with wealth, victories, growth
        private static bool isWaiting = false;
        private static float lastNarrationTime = -999f;
        private static List<string> eventProcessing = new List<string>();
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
                            Log.Error(msg);
                        else if (level == "warning")
                            Log.Warning(msg);
                        else
                            LogManager.Log(msg);
                        pendingLog.RemoveAt(0);
                    }
                }
            }
            

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
            LogManager.Log($"Decaying mood. Before decay - Stress: {moodStress}, Chaos: {moodChaos}, Sympathy: {moodSympathy}, Confidence: {moodConfidence}");
            float decay = 0.001f; // slow decay per frame

            moodStress = Mathf.Max(0f, moodStress - decay);
            moodChaos = Mathf.Max(0f, moodChaos - decay);
            moodSympathy = Mathf.Max(0f, moodSympathy - decay);
            moodConfidence = Mathf.Max(0f, moodConfidence - decay);
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

            LogManager.Log(
                "[LivingStoryteller] Event intercepted: " + eventKey);
            var settings = ModOptions.Settings;

            if (settings.ApiKey.NullOrEmpty())
            {
                Log.Warning( "[LivingStoryteller] No API key configured. " + "Go to Mod Settings > The Living Storyteller.");
                eventProcessing.Remove(eventKey);
                return;
            }

            if (Time.time - lastNarrationTime < settings.cooldownSeconds)
            {
                LogManager.Log("Cooldown active. Skipping narration for event: " + incidentLabel + " (Category: " + incidentCategory + ")");
                eventProcessing.Remove(eventKey);
                return;
            }

            if (isWaiting)
            {
                LogManager.Log("Already waiting for a narration response. " + "Skipping new narration for event: " + incidentLabel + " (Category: " + incidentCategory + ")");
                eventProcessing.Remove(eventKey);
                return;
            }

            lastNarrationTime = Time.time;
            isWaiting = true;

            // Cache portrait on MAIN THREAD
            Texture2D portrait = GetStorytellerPortrait();



            string systemPrompt = persona + settings.PersonaText;
            string userMessage = $"Event:{incidentLabel} (Category:{ incidentCategory})"+ (colonyContext.NullOrEmpty() ? "" : "\n" + colonyContext);
            string emotion = string.Empty;
            string mood = string.Empty;
            LogManager.Log($"Use Emotion: {settings.UseEmotion}");
            if (settings.UseEmotion)
            {
                emotion = GetEmotion(incidentCategory, incidentLabel, PersonaDefName);
                LogManager.Log($"emotion: {emotion}");
                mood = GetMoodDescriptor(PersonaDefName);
                LogManager.Log($"mood: {mood}");
                UpdateMood(incidentCategory, incidentLabel);

                systemPrompt += $"\nCurrent emotional tone:{emotion}.";
                systemPrompt += $"\nCurrent storyteller mood:{mood}.";

                systemPrompt += "\nAdjust your narration style to reflect both the immediate emotion and the long-term mood.";

                userMessage += $",\nEmotional tone:{emotion}";
                userMessage += $"\nmood:{mood}.";

            }
            LogManager.Log($"Use Accent: {settings.UseAccent}");
            if (settings.UseAccent)
            {
                var accent = StorytellerPersonaDatabase.GetAccent(PersonaDefName);
                LogManager.Log($"accent: {accent}");
                systemPrompt += $"\nUse Accent:{accent}.";
                userMessage += $",\nAccent:{accent}";
            }

            string name = storytellerName;
            string endpoint = settings.Endpoint;
            string apiKey = settings.ApiKey.Trim();
            string model = settings.ModelName;

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
                            if(settings.TTSEnabled) TTSService.RequestSpeech(response, PersonaDefName, emotion, mood);
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
                eventProcessing.Remove(eventKey);
            });
        }

        private static void UpdateMood(string category, string label)
        {
            LogManager.Log($"Updating mood based on event. Category: {category}, Label: {label}");
            // Pawn death → sympathy + stress
            if (label.Contains("Died") || category == "PawnDeath")
            {
                moodSympathy += 0.4f;
                moodStress += 0.2f;
            }

            // Big threats → chaos + stress
            if (category == "ThreatBig")
            {
                moodChaos += 0.5f;
                moodStress += 0.3f;
            }

            // Small threats → chaos
            if (category == "ThreatSmall" || category == "MajorThreat")
            {
                moodChaos += 0.3f;
            }

            // Positive events → confidence
            if (label.Contains("Inspired") || label.Contains("Marriage") || label.Contains("Birth"))
            {
                moodConfidence += 0.4f;
            }

            // Clamp values
            moodStress = Mathf.Clamp(moodStress, 0f, 5f);
            moodChaos = Mathf.Clamp(moodChaos, 0f, 5f);
            moodSympathy = Mathf.Clamp(moodSympathy, 0f, 5f);
            moodConfidence = Mathf.Clamp(moodConfidence, 0f, 5f);
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
            LogManager.Log($"Determining mood descriptor for personaDef: {personaDef}. Current mood values - Stress: {moodStress}, Chaos: {moodChaos}, Sympathy: {moodSympathy}, Confidence: {moodConfidence}");
            var mood = "neutral";
            if (moodStress > 3f) mood = "anxious";
            if (moodChaos > 3f) mood = "chaotic";
            if (moodSympathy > 3f) mood = "somber";
            if (moodConfidence > 3f) mood = "confident";

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
    }
}
