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
        private  static HttpClient httpClient = new HttpClient();
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
        private static string pendingLog;
        private static string pendingLogLevel;

        public static void ProcessPending()
        {
            // Process pending logs
            lock (logLock)
            {
                if (pendingLog != null)
                {
                    string msg = pendingLog;
                    string level = pendingLogLevel;
                    pendingLog = null;
                    pendingLogLevel = null;

                    if (level == "error")
                        Log.Error(msg);
                    else if (level == "warning")
                        Log.Warning(msg);
                    else
                        LogManager.Log(msg);
                }
            }

            if(TTSService.ProcessingAudio)
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

        private static void QueueLog(
            string message, string level = "message")
        {
            lock (logLock)
            {
                pendingLog = message;
                pendingLogLevel = level;
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
            if (settings.UseAccent)
            {
                var accent = StorytellerPersonaDatabase.GetAccent(PersonaDefName);
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
                            TTSService.RequestSpeech(response, PersonaDefName, emotion, mood);
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
            endpoint = endpoint.Trim();
            if (!endpoint.StartsWith("http"))
            {
                endpoint = "https://" + endpoint;
            }

            string json =
                "{\"model\":\"" + EscapeJson(model) + "\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" +
                    EscapeJson(systemPrompt) + "\"}," +
                "{\"role\":\"user\",\"content\":\"{" +
                    EscapeJson(userMessage) + "\"}" +
                "]," +
                "\"max_tokens\":8192," +
                "\"temperature\":0.9}";

            QueueLog("Sending API request to " + endpoint + " with model: " + json);
            var client = httpClient;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try 
            { 
                using (var resp = client.PostAsync(endpoint, content).Result)
                {
                    resp.EnsureSuccessStatusCode();
                    string responseBody = resp.Content.ReadAsStringAsync().Result;
                    QueueLog("[LivingStoryteller] Raw API response: " + responseBody);
                    // Debug logging via queue
                    string preview = responseBody.Length > 500
                        ? responseBody.Substring(0, 500) + "..."
                        : responseBody;

                    QueueLog("[LivingStoryteller] Raw API response: " + responseBody);

                    return ParseContent(responseBody);
                }                        
            }
            catch (WebException wex)
            {
                var httpResp =
                    wex.Response as HttpWebResponse;
                if (httpResp != null &&
                    (int)httpResp.StatusCode == 429)
                {
                    QueueLog("[LivingStoryteller] Rate limited. " + "Skipping this narration.", "warning");
                    return null;
                }
                throw;
            }
            
            //try
            //{
            //    var request =
            //        (HttpWebRequest)WebRequest.Create(endpoint);
            //    request.Method = "POST";
            //    request.ContentType = "application/json";
            //    request.Headers["Authorization"] =
            //        "Bearer " + apiKey;
            //    request.Timeout = 30000;

            //    using (var writer = new StreamWriter(
            //        request.GetRequestStream()))
            //    {
            //        writer.Write(json);
            //    }

            //    string responseBody;
            //    using (var response =
            //        (HttpWebResponse)request.GetResponse())
            //    using (var reader = new StreamReader(
            //        response.GetResponseStream()))
            //    {
            //        responseBody = reader.ReadToEnd();
            //    }

            //    // Debug logging via queue
            //    string preview = responseBody.Length > 500
            //        ? responseBody.Substring(0, 500) + "..."
            //        : responseBody;

            //    QueueLog( "[LivingStoryteller] Raw API response: " + responseBody);

            //    return ParseContent(responseBody);
            //}
            //catch (WebException wex)
            //{
            //    var httpResp =
            //        wex.Response as HttpWebResponse;
            //    if (httpResp != null &&
            //        (int)httpResp.StatusCode == 429)
            //    {
            //        QueueLog("[LivingStoryteller] Rate limited. " + "Skipping this narration.", "warning");
            //        return null;
            //    }
            //    throw;
            //}
        }

        private static string ParseContent(string json)
        {
            // Find the first "content" field in the response
            int contentIdx = json.IndexOf("\"content\"");
            if (contentIdx < 0) return null;

            // Find the colon
            int colonIdx = json.IndexOf(':', contentIdx + 9);
            if (colonIdx < 0) return null;

            // Find the opening quote of the value
            int openQuote = json.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return null;

            // Walk character by character
            var sb = new StringBuilder();
            int i = openQuote + 1;
            while (i < json.Length)
            {
                char c = json[i];

                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 5 < json.Length)
                            {
                                string hex = json.Substring(i + 2, 4);
                                if (int.TryParse(hex,
                                    System.Globalization
                                        .NumberStyles.HexNumber,
                                    null, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 6;
                                    continue;
                                }
                            }
                            sb.Append('\\');
                            sb.Append(next);
                            break;
                        default:
                            sb.Append('\\');
                            sb.Append(next);
                            break;
                    }
                    i += 2;
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            string result = sb.ToString().Trim();

            if (result.Length == 0) return null;

            return result;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
