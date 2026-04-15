using Extension.LivingStoryTeller;
using Google.GenAI.Types;
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
                    LogManager.Log(persona + " is already processing a narration for this event. Skipping duplicate request.");
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

            string userMessage = "Event: " + incidentLabel +
                " (Category: " + incidentCategory + ")" +
                (colonyContext.NullOrEmpty()
                    ? "" : "\n" + colonyContext);

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
                            TTSService.RequestSpeech(response, PersonaDefName);
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
                "{\"role\":\"user\",\"content\":\"" +
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
