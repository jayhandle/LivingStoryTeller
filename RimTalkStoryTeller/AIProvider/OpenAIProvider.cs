using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace LivingStoryteller
{
    internal class OpenAIProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public string JSONRequest(string text, string voiceType)
        {
            var json = new
            {
                model = ModOptions.Settings.TTSModelName,
                voice = voiceType,
                input = text,
            };

            string jsonString = JsonConvert.SerializeObject(json);
            return jsonString;
        }

        public async Task<string> GetResponse(string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = ModOptions.Settings.TTSEndpoint;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ModOptions.Settings.ApiKey);
                LogManager.Log($"[TTS] Making request to OpenAI TTS endpoint: {url}: with content: {json}");
            using (var resp = await httpClient.PostAsync(ModOptions.Settings.Endpoint, content))
            {
                resp.EnsureSuccessStatusCode();
                string responseBody = await resp.Content.ReadAsStringAsync();
                LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);
                
                return responseBody;
            }
        }
    }
}