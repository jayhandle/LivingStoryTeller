using Google.GenAI.Types;
using System.Net;
using System.Net.Http;
using System.Text;

namespace LivingStoryteller
{
    internal class GoogleProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const int MaxTtsAttempts = 3;

        public async Task<TTSResponseData> GetTTSResponse(string json)
        {
            // 1. Separate the base endpoint URL from the API key
            var baseUrl = ModOptions.Settings.TTSEndpoint; // e.g., "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent"
            var apiKey = ModOptions.Settings.ApiKey;

            LogManager.Log($"[TTS] Making request to Google TTS endpoint: {baseUrl}. Payload length = {json?.Length ?? 0}");

            for (int attempt = 1; attempt <= MaxTtsAttempts; attempt++)
            {
                // 2. Use HttpRequestMessage to explicitly configure headers per request
                using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl))
                {
                    // Clear any lingering Bearer/OAuth authorization headers on the request
                    request.Headers.Authorization = null;

                    // Set the Google API Key header explicitly
                    request.Headers.Add("x-goog-api-key", apiKey);

                    // Attach the JSON payload
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var resp = await httpClient.SendAsync(request))
                    {
                        if (resp.StatusCode == (HttpStatusCode)429)
                        {
                            string errorBody = await resp.Content.ReadAsStringAsync();

                            if (attempt == MaxTtsAttempts)
                            {
                                LogManager.Error("[TTS] Google rate limit reached after retries. " +
                                    "Last status: 429, body preview: " + MakePreview(errorBody, 500));
                                throw new HttpRequestException("Google TTS rate limited (429) after retries.");
                            }

                            var delay = GetRetryDelay(resp, attempt);
                            LogManager.Warning($"[TTS] Google TTS rate limited (429). " +
                                $"Retrying in {(int)delay.TotalMilliseconds}ms (attempt {attempt}/{MaxTtsAttempts}).");
                            await Task.Delay(delay);
                            continue;
                        }

                        if (!resp.IsSuccessStatusCode)
                        {
                            string errorBody = await resp.Content.ReadAsStringAsync();
                            LogManager.Error("[TTS] Google TTS request failed. Status: " + resp.StatusCode +
                                ", body preview: " + MakePreview(errorBody, 500));
                            resp.EnsureSuccessStatusCode();
                        }

                        string responseBody = await resp.Content.ReadAsStringAsync();
                        LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);
                        var pcmData = ExtractInlinePCM(responseBody);

                        return new TTSResponseData(pcmData);
                    }
                }
            }

            throw new HttpRequestException("Google TTS request did not complete successfully.");
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage resp, int attempt)
        {
            var retryAfter = resp.Headers?.RetryAfter;
            if (retryAfter != null)
            {
                if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
                    return retryAfter.Delta.Value;

                if (retryAfter.Date.HasValue)
                {
                    var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero)
                        return delta;
                }
            }

            // Exponential backoff with modest caps to avoid blocking game flow too long.
            int delayMs = Math.Min(8000, 1000 * (1 << (attempt - 1)));
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private static string MaskApiKeyInUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            const string keyToken = "key=";
            int keyIdx = url.IndexOf(keyToken, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0)
                return url;

            int valueStart = keyIdx + keyToken.Length;
            int valueEnd = url.IndexOf('&', valueStart);
            if (valueEnd < 0)
                valueEnd = url.Length;

            return url.Substring(0, valueStart) + "***" + url.Substring(valueEnd);
        }

        private static string MakePreview(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
                return "<empty>";

            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        private static byte[] ExtractInlinePCM(string responseBody)
        {
            // Find "inlineData"
            int inlineIdx = responseBody.IndexOf("\"inlineData\"");
            if (inlineIdx < 0)
                return null;

            // Find "data" inside inlineData
            int dataIdx = responseBody.IndexOf("\"data\"", inlineIdx);
            if (dataIdx < 0)
                return null;

            // Find the first quote after "data":
            int start = responseBody.IndexOf('"', dataIdx + 6) + 1;
            int end = responseBody.IndexOf('"', start);

            if (start < 0 || end < 0)
                return null;

            string base64 = responseBody.Substring(start, end - start);
            string base64Preview = base64.Length > 10 ? base64.Substring(0, 10) : base64;
            LogManager.Log("[TTS] Extracted base64 PCM length = " + base64.Length + " substring: " + base64Preview);
            return Convert.FromBase64String(base64);
        }


        public string JSONTTSRequest(string text, string personaDef, string voice, string emotion, string mood)
        {
            var promptBuilder = $"{StorytellerPersonaDatabase.GetPersonaText(personaDef)}.";
            if (ModOptions.Settings.UseAccent) promptBuilder += $" Your accent is {StorytellerPersonaDatabase.GetAccent(personaDef)}.";
            if (ModOptions.Settings.UseEmotion) promptBuilder += $" Your emotional tone is {emotion}. Your mood is {mood}";

            string json =
                $@"{{""contents"":
                    [
                        {{""parts"":
                            [{{""text"": ""{promptBuilder}. Say:{text}""
                            }}]
                        }}
                    ],
                    ""generationConfig"": 
                    {{ ""responseModalities"":[""AUDIO""], 
                        ""speechConfig"": 
                        {{""voiceConfig"": 
                            {{ ""prebuiltVoiceConfig"": 
                                {{ ""voiceName"": ""{voice}"" 
                                }}
                            }}
                        }}
                    }},
                ""model"":""{ModOptions.Settings.TTSModelName}""
                }}";
            return json;
        }

        public async Task<string> GetResponse(string json)
        {
            var endpoint = ModOptions.Settings.Endpoint;

            // Ensure proper scheme formatting
            if (!endpoint.StartsWith("http://") && !endpoint.StartsWith("https://"))
            {
                endpoint = "https://" + endpoint;
            }

            var apiKey = ModOptions.Settings.ApiKey;
            var client = httpClient;
            client.Timeout = TimeSpan.FromSeconds(30);

            // Clear headers and set the Google-specific API key header
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using (var resp = await client.PostAsync(endpoint, content))
                {
                    // Catch 429 specifically for HttpClient (PostAsync throws HttpRequestException, not WebException)
                    if (resp.StatusCode == (HttpStatusCode)429)
                    {
                        LogManager.Warning("[LivingStoryteller] Rate limited. Skipping this narration.");
                        return null;
                    }

                    resp.EnsureSuccessStatusCode();

                    string responseBody = await resp.Content.ReadAsStringAsync();

                    // Debug logging
                    string preview = responseBody.Length > 500
                        ? responseBody.Substring(0, 500) + "..."
                        : responseBody;

                    LogManager.Log("Raw API response: " + preview);

                    return ParseContent(responseBody);
                }
            }
            catch (HttpRequestException ex)
            {
                LogManager.Error("[LivingStoryteller] Chat API request failed: " + ex.Message);
                throw;
            }
        }

        public string JSONRequest(string model, string systemPrompt, string userMessage)
        {
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

            LogManager.Log($"Sending request json:{json}");

            return json;

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

    }
}