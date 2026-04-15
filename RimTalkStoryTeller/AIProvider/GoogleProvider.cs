using System.Net.Http;
using System.Text;

namespace LivingStoryteller
{
    internal class GoogleProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public GoogleProvider()
        {
        }

        public async Task<string> GetResponse(string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = ModOptions.Settings.TTSEndpoint + ModOptions.Settings.ApiKey;
            LogManager.Log($"[TTS] Making request to Google TTS endpoint: {url}: with content: {json}");
            using (var resp = await httpClient.PostAsync(url, content))
            {
                resp.EnsureSuccessStatusCode();
                string responseBody = await resp.Content.ReadAsStringAsync();
                LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);

                return responseBody;
            }
        }

        public string JSONRequest(string text, string voice)
        {
            string json =
                "{ \"contents\":[{\"parts\":[{\"text\": \"" + text + "\"}]}]," +
                "\"generationConfig\": { \"responseModalities\":[\"AUDIO\"], \"speechConfig\": { \"voiceConfig\": { \"prebuiltVoiceConfig\": { \"voiceName\": \"" + voice + "\" }}}}," +
                "\"model\":\""+ModOptions.Settings.TTSModelName+"\"" +
                "}";
            return json;
        }
    }
}