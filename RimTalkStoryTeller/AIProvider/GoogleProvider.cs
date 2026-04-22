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
            //httpClient.DefaultRequestHeaders.Clear();
            //httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ModOptions.Settings.ApiKey);
            //httpClient.DefaultRequestHeaders.Add("x-goog-api-key", ModOptions.Settings.ApiKey);
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            using (var resp = await httpClient.PostAsync(url, content))
            {
                resp.EnsureSuccessStatusCode();
                string responseBody = await resp.Content.ReadAsStringAsync();
                LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);

                return responseBody;
            }
        }


        public string JSONRequest(string text, string personaDef, string voice, string emotion, string mood)
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
        

            //var json =
            //$@"{{
            //    ""audioConfig"":
            //    {{
            //        ""audioEncoding"" : ""LINEAR16"",
            //        ""pitch"" : 0,
            //        ""speakingRate"" : 1
            //    }},
            //    ""input"":
            //    {{
            //        ""prompt"" : ""{promptBuilder}"",
            //        ""text"" : ""{text}""
            //    }},
            //        ""voice"" : {{
            //        ""languageCode"" : ""en-us"",
            //        ""name"" : ""{voice}"",
            //        ""modelName"" : ""{ModOptions.Settings.TTSModelName}""
            //    }}
            //}}";
            // return json;
        }
//        public string JSONRequest(string text, string voice)
//        {
//            string json =
//                $@"{{""contents"":
//[
//    {{""parts"":
//        [""text"": ""{text}""]
//    }}
//]," +
//                "\"generationConfig\": { \"responseModalities\":[\"AUDIO\"], \"speechConfig\": { \"voiceConfig\": { \"prebuiltVoiceConfig\": { \"voiceName\": \"" + voice + "\" }}}}," +
//                "\"model\":\""+ModOptions.Settings.TTSModelName+"\"" +
//                "}";
//            return json;
//        }
    }
}