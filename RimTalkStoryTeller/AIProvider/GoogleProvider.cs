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
            var baseUrl = ModOptions.Settings.TTSEndpoint;
            var apiKey = ModOptions.Settings.EffectiveTTSApiKey;

            LogManager.Log($"[TTS] Making request to Google TTS endpoint: {baseUrl}. Payload length = {json?.Length ?? 0}");

            for (int attempt = 1; attempt <= MaxTtsAttempts; attempt++)
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    LogManager.Error("[TTS] Google TTS API key is null or empty. Please set a valid API key in the mod settings.");
                    throw new InvalidOperationException("Google TTS API key is required but not provided.");
                }

                // Clear any lingering Bearer/OAuth authorization headers on the request
                var client = httpClient;
                client.Timeout = TimeSpan.FromSeconds(30);

                // Use auth header style expected by the target endpoint.
                bool useOpenAiCompat = baseUrl.IndexOf("/openai/", StringComparison.OrdinalIgnoreCase) >= 0;
                client.DefaultRequestHeaders.Clear();
                if (useOpenAiCompat)
                {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
                }
                else
                {
                    client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                }

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Attach the JSON payload

                using (var resp = await client.PostAsync(baseUrl, content))
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
                            ", body preview: " + MakePreview(errorBody, 5000));
                        resp.EnsureSuccessStatusCode();
                    }

                    string responseBody = await resp.Content.ReadAsStringAsync();
                    LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);
                    var pcmData = ExtractInlinePCM(responseBody);

                    return new TTSResponseData(pcmData);
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

        private static string MakePreview(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
                return "<empty>";

            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        private static byte[] ExtractInlinePCM(string responseBody)
        {
            LogManager.Log("[TTS] ExtractInlinePCM called. Response body length = " + responseBody.Length);
            // Find "inlineData"
            int inlineIdx = responseBody.IndexOf("\"content\"");
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
            // Strip newlines and carriage returns directly
            var cleanPersona = (StorytellerPersonaDatabase.GetPersonaText(personaDef) ?? "")
                .Replace("\r", " ")
                .Replace("\n", "");

            var cleanText = text.Replace("\r", " ").Replace("\n", "");

            var promptBuilder = cleanPersona;
            if (!promptBuilder.EndsWith(".")) promptBuilder += ".";

            if (ModOptions.Settings.UseAccent)
                promptBuilder += $" Your accent is {StorytellerPersonaDatabase.GetAccent(personaDef)}.";

            if (ModOptions.Settings.UseEmotion)
                promptBuilder += $" Your emotional tone is {emotion}. Your mood is {mood}.";

            // Escape interior quotes to safeguard JSON structure
            promptBuilder = promptBuilder.Replace("\"", "\\\"");
            cleanText = cleanText.Replace("\"", "\\\"");

            string json = $@"{{
    ""model"": ""{ModOptions.Settings.TTSModelName}"",
    ""input"": ""{promptBuilder} Say: {cleanText}"",
    ""response_format"": {{
        ""type"": ""audio""
    }},
    ""generation_config"": {{
      ""speech_config"": [
        {{ ""voice"": ""{voice}"" }}
      ]
    }}    
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

            // Use auth header style expected by the target endpoint.
            bool useOpenAiCompat = endpoint.IndexOf("/openai/", StringComparison.OrdinalIgnoreCase) >= 0;
            client.DefaultRequestHeaders.Clear();
            if (useOpenAiCompat)
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            }
            else
            {
                client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            }

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using (var resp = await client.PostAsync(endpoint, content))
                {
                    string responseBody = await resp.Content.ReadAsStringAsync();

                    // Catch 429 specifically for HttpClient (PostAsync throws HttpRequestException, not WebException)
                    if (resp.StatusCode == (HttpStatusCode)429)
                    {
                        LogManager.Warning("[LivingStoryteller] Rate limited. Skipping this narration.");
                        return null;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        string errorPreview = responseBody.Length > 1000
                            ? responseBody.Substring(0, 1000) + "..."
                            : responseBody;

                        LogManager.Error("[LivingStoryteller] Chat API request failed. Status=" + resp.StatusCode +
                            ", endpoint=" + endpoint +
                            ", authMode=" + (useOpenAiCompat ? "Authorization Bearer" : "x-goog-api-key") +
                            ", body preview=" + errorPreview);

                        resp.EnsureSuccessStatusCode();
                    }

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
            "\"system_instruction\":\"" + EscapeJson(systemPrompt) + "\"," +
            "\"input\":\"" + EscapeJson(userMessage) + "\"," +
            "\"generation_config\":{" +
            "\"temperature\":0.9" +
            "}}";

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
            // Find the model_output step specifically
            int outputIdx = json.IndexOf("\"content\"");
            if (outputIdx < 0) return null;

            // Find the "text" field that appears after model_output
            int textIdx = json.IndexOf("\"text\"", outputIdx);
            if (textIdx < 0) return null;

            // Find the colon after "text"
            int colonIdx = json.IndexOf(':', textIdx + 6);
            if (colonIdx < 0) return null;

            // Find the opening quote of the value
            int openQuote = json.IndexOf('"', colonIdx + 1);
            if (openQuote < 0) return null;

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
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
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
                    break; // End of string
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            string result = sb.ToString().Trim();
            return result.Length == 0 ? null : result;
        }

    }
}