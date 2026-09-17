using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace LivingStoryteller
{
    internal class CustomProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public string JSONTTSRequest(string text, string personaDef, string voiceType, string emotion, string mood)
        {
            var settings = ModOptions.Settings;
            if (settings.CustomTTSMode != StorytellerSettings.CustomTTSResponseMode.legacy)
            {
                return ApplyRequestTemplate(
                    settings.CustomTTSRequestTemplate,
                    text,
                    personaDef,
                    voiceType,
                    emotion,
                    mood);
            }

            string jsonString =
                "{\"model\":\"" + EscapeJson(ModOptions.Settings.TTSModelName) + "\"," +
                "\"voice\":\"" + EscapeJson(voiceType ?? string.Empty) + "\"," +
                "\"input\":\"" + EscapeJson(text) + "\"}";
            return jsonString;
        }

        public async Task<TTSResponseData> GetTTSResponse(string json)
        {
            if (ModOptions.Settings.CustomTTSMode != StorytellerSettings.CustomTTSResponseMode.legacy)
            {
                return await GetConfiguredTTSResponse(json);
            }

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = ModOptions.Settings.TTSEndpoint;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ModOptions.Settings.EffectiveTTSApiKey);
            LogManager.Log($"[TTS] Making request to custom TTS endpoint: {url}: with content: {json}");
            using (var resp = await httpClient.PostAsync(url, content))
            {
                resp.EnsureSuccessStatusCode();
                string responseBody = await resp.Content.ReadAsStringAsync();
                LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);
                var bArry = Encoding.UTF8.GetBytes(responseBody); //needs to be tested with actual TTS response, may need to be base64 decoded or something else depending on the API
                return new TTSResponseData(bArry);
            }
        }

        private static async Task<TTSResponseData> GetConfiguredTTSResponse(string requestBody)
        {
            var settings = ModOptions.Settings;
            byte[] audioData;

            using (var request = new HttpRequestMessage(HttpMethod.Post, settings.TTSEndpoint))
            {
                AddAuthentication(request);
                request.Content = new StringContent(
                    requestBody,
                    Encoding.UTF8,
                    string.IsNullOrWhiteSpace(settings.CustomTTSContentType)
                        ? "application/json"
                        : settings.CustomTTSContentType);
                LogManager.Log($"[TTS] Making configured custom TTS request to: {settings.TTSEndpoint}");

                using (var response = await httpClient.SendAsync(request))
                {
                    if (settings.CustomTTSMode == StorytellerSettings.CustomTTSResponseMode.direct_audio)
                    {
                        audioData = await response.Content.ReadAsByteArrayAsync();
                        response.EnsureSuccessStatusCode();
                    }
                    else
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();

                        string downloadLocation = BuildDownloadLocation(responseJson);
                        Uri downloadUrl = ResolveDownloadUrl(settings.TTSEndpoint, downloadLocation);

                        using (var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                        {
                            AddAuthentication(downloadRequest);
                            using (var downloadResponse = await httpClient.SendAsync(downloadRequest))
                            {
                                audioData = await downloadResponse.Content.ReadAsByteArrayAsync();
                                downloadResponse.EnsureSuccessStatusCode();
                                LogManager.Log("[TTS] Downloaded custom audio bytes = " + audioData.Length);
                            }
                        }
                    }
                }
            }

            return ConvertAudioResponse(audioData);
        }

        private static void AddAuthentication(HttpRequestMessage request)
        {
            var settings = ModOptions.Settings;
            if (!string.IsNullOrWhiteSpace(settings.CustomTTSHeaderName) &&
                !string.IsNullOrWhiteSpace(settings.EffectiveTTSApiKey))
            {
                request.Headers.TryAddWithoutValidation(
                    settings.CustomTTSHeaderName,
                    (settings.CustomTTSHeaderPrefix ?? string.Empty) + settings.EffectiveTTSApiKey);
            }
        }

        private static TTSResponseData ConvertAudioResponse(byte[] audioData)
        {
            switch (ModOptions.Settings.CustomTTSAudioType)
            {
                case StorytellerSettings.CustomTTSAudioFormat.wav:
                    return new TTSResponseData(ConvertWavToPlaybackPcm(audioData));
                case StorytellerSettings.CustomTTSAudioFormat.mp3:
                    return new TTSResponseData(audioData, "mpeg");
                default:
                    return new TTSResponseData(audioData);
            }
        }

        private static byte[] ConvertWavToPlaybackPcm(byte[] wavData)
        {
            using (var input = new System.IO.MemoryStream(wavData))
            using (var reader = new WaveFileReader(input))
            {
                var targetFormat = new WaveFormat(24000, 16, 1);
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                using (var output = new System.IO.MemoryStream())
                {
                    resampler.ResamplerQuality = 60;
                    var buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                    }
                    return output.ToArray();
                }
            }
        }

        private static string ApplyRequestTemplate(
            string template,
            string text,
            string persona,
            string voice,
            string emotion,
            string mood)
        {
            var settings = ModOptions.Settings;
            return (template ?? string.Empty)
                .Replace("{text}", EscapeTemplateValue(text))
                .Replace("{persona}", EscapeTemplateValue(persona))
                .Replace("{voice}", EscapeTemplateValue(voice))
                .Replace("{emotion}", EscapeTemplateValue(emotion))
                .Replace("{mood}", EscapeTemplateValue(mood))
                .Replace("{language}", EscapeTemplateValue(settings.CustomTTSLanguage))
                .Replace("{model}", EscapeTemplateValue(settings.TTSModelName));
        }

        private static string EscapeTemplateValue(string value)
        {
            if (string.Equals(
                ModOptions.Settings.CustomTTSContentType,
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase))
            {
                return Uri.EscapeDataString(value ?? string.Empty);
            }

            return EscapeJson(value);
        }

        private static string BuildDownloadLocation(string responseJson)
        {
            var settings = ModOptions.Settings;
            if (string.IsNullOrWhiteSpace(settings.CustomTTSDownloadUrlTemplate))
            {
                string? path = ParseJsonString(responseJson, settings.CustomTTSDownloadPathField);
                if (path == null || string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException(
                        "The custom TTS response did not contain '" + settings.CustomTTSDownloadPathField + "'.");
                }
                return path;
            }

            string result = settings.CustomTTSDownloadUrlTemplate;
            int openingBrace = result.IndexOf('{');
            while (openingBrace >= 0)
            {
                int closingBrace = result.IndexOf('}', openingBrace + 1);
                if (closingBrace < 0) break;

                string fieldName = result.Substring(openingBrace + 1, closingBrace - openingBrace - 1);
                string? fieldValue = ParseJsonString(responseJson, fieldName);
                if (fieldValue == null)
                {
                    throw new InvalidOperationException(
                        "The custom TTS response did not contain '" + fieldName + "'.");
                }

                result = result.Substring(0, openingBrace) + fieldValue + result.Substring(closingBrace + 1);
                openingBrace = result.IndexOf('{', openingBrace + fieldValue.Length);
            }
            return result;
        }

        private static Uri ResolveDownloadUrl(string endpoint, string downloadLocation)
        {
            if (Uri.TryCreate(downloadLocation, UriKind.Absolute, out Uri absoluteUrl))
            {
                return absoluteUrl;
            }

            return new Uri(new Uri(endpoint), downloadLocation);
        }

        private static string? ParseJsonString(string json, string propertyName)
        {
            string marker = "\"" + propertyName + "\"";
            int propertyIndex = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (propertyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', propertyIndex + marker.Length);
            if (colonIndex < 0) return null;

            int quoteIndex = json.IndexOf('"', colonIndex + 1);
            if (quoteIndex < 0) return null;

            var value = new StringBuilder();
            bool escaped = false;
            for (int index = quoteIndex + 1; index < json.Length; index++)
            {
                char current = json[index];
                if (escaped)
                {
                    value.Append(current == '/' ? '/' : current);
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    return value.ToString();
                }
                else
                {
                    value.Append(current);
                }
            }

            return null;
        }

        public async Task<string> GetResponse(string json)
        {
            var endpoint = ModOptions.Settings.Endpoint;

            var apiKey = ModOptions.Settings.ApiKey;
            var client = httpClient;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                using (var resp = await client.PostAsync(endpoint, content))
                {
                    resp.EnsureSuccessStatusCode();
                    string responseBody = await resp.Content.ReadAsStringAsync();
                    LogManager.Log("Raw API response: " + responseBody);
                    // Debug logging via queue
                    string preview = responseBody.Length > 500
                        ? responseBody.Substring(0, 500) + "..."
                        : responseBody;

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
                    LogManager.Warning("[LivingStoryteller] Rate limited. " + "Skipping this narration.");
                    return null;
                }
                throw;
            }
        }

        public string JSONRequest(string model, string systemPrompt, string userMessage)
        {
            string json =
                "{\"model\":\"" + EscapeJson(model) + "\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + EscapeJson(systemPrompt) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJson(userMessage) + "\"}" +
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
