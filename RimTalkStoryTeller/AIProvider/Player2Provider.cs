using System.Net;
using System.Net.Http;
using System.Text;

namespace LivingStoryteller
{
    internal class Player2Provider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task<string> GetResponse(string json)
        {
            var endpoint = ModOptions.Settings.Endpoint;
            if (!endpoint.StartsWith("http"))
            {
                endpoint = endpoint.Replace("http://", "");
                endpoint = "https://" + endpoint;
            }

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

                    LogManager.Log("Raw API response: " + preview);

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

        public async Task<string> GetTTSResponse(string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = ModOptions.Settings.TTSEndpoint;
            LogManager.Log($"[TTS] Making request to Player2 TTS endpoint: {url}: with content: {json}");
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ModOptions.Settings.ApiKey);
            httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            using (var resp = await httpClient.PostAsync(url, content))
            {
                resp.EnsureSuccessStatusCode();
                string responseBody = await resp.Content.ReadAsStringAsync();
                LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);

                return responseBody;
            }
        }

        public string JSONRequest(string model, string systemPrompt, string userMessage)
        {
            var json = $@"{{
    ""messages"": [
        {{
            ""content"": ""{systemPrompt}"",
            ""role"": ""system""
        }},
        {{
            ""content"": ""{userMessage}"",
            ""role"": ""user""
        }}
    ],
    ""stream"": false
}}";
            return json;
        }

        public string JSONTTSRequest(string text, string personaDef, string voice, string emotion, string mood)
        {
            var promptBuilder = $"{StorytellerPersonaDatabase.GetPersonaText(personaDef)}.";
            var gender = StorytellerPersonaDatabase.GetGender(personaDef);
            if (ModOptions.Settings.UseAccent) promptBuilder += $" Your accent is {StorytellerPersonaDatabase.GetAccent(personaDef)}.";
            if (ModOptions.Settings.UseEmotion) promptBuilder += $" Your emotional tone is {emotion}. Your mood is {mood}";

            string json =
                $@"{{
""text"": ""{text}"",
""voice_ids"": [

    ""{voice}""

],
""speed"": 0.25,
""audio_format"": ""mp3"",
""voice_gender"": ""{gender}"",
""voice_language"": ""en_US"",
""advanced_voice"": {{

    ""instructions"": ""{promptBuilder}""

}},
""disable_advanced"": true
}}";
            return json;
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