using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryteller
{
    //https://api.novelai.net/docs/#/%2Fai%2F/AIController_aiGenerate
    internal class NovelAIProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public string JSONTTSRequest(string text, string personaDef, string voiceType, string emotion, string mood)
        {
            text = text.Replace("\n", "");
            text = text.Replace("\"", "");
            text = text.Replace("\t", "");
            text = text.Replace("\\", "");
            var json = $@"
{{
  ""text"": ""{EscapeJson(text)}"",
  ""seed"": ""{voiceType??"default"}"",
  ""voice"": -1,
  ""opus"": true,
  ""version"": ""v2""
}}
";
            return json;
        }

        public async Task<TTSResponseData> GetTTSResponse(string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = ModOptions.Settings.TTSEndpoint;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ModOptions.Settings.EffectiveTTSApiKey);
            LogManager.Log($"[TTS] Making request to NovelAI TTS endpoint: {url}: with content: {json}");
            using (var resp = await httpClient.PostAsync(url, content))
            {
                var responseBody = await resp.Content.ReadAsByteArrayAsync();
                LogManager.Log("Raw API response count: " + responseBody.Count());
                resp.EnsureSuccessStatusCode();
                return new TTSResponseData(responseBody, "mpeg");
            }
        }

        public async Task<string> GetResponse(string json)
        {
            var endpoint = ModOptions.Settings.Endpoint;

            var apiKey = ModOptions.Settings.ApiKey;
            var client = httpClient;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            LogManager.Log($"Sending request json:{json}\nTo endpoint {endpoint}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                using (var resp = await client.PostAsync(endpoint, content))
                {
                    string responseBody = await resp.Content.ReadAsStringAsync();
                    LogManager.Log("Raw API response: " + responseBody);
                    resp.EnsureSuccessStatusCode();
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
            int outputIdx = json.IndexOf("\"output\"");
if (outputIdx >= 0 && contentIdx < 0 )
            {
                contentIdx = outputIdx;
            }
            if (contentIdx < 0)
            { 
                return null; 
            }

            // Find the colon
            int colonIdx = json.IndexOf(':', contentIdx);
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
