using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
            if (wavData == null || wavData.Length < 12 ||
                Encoding.ASCII.GetString(wavData, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(wavData, 8, 4) != "WAVE")
            {
                throw new InvalidOperationException("The custom TTS response is not a valid RIFF WAV file.");
            }

            ushort format = 0;
            ushort channels = 0;
            int sampleRate = 0;
            ushort bitsPerSample = 0;
            int dataOffset = -1;
            int dataLength = 0;

            int chunkOffset = 12;
            while (chunkOffset + 8 <= wavData.Length)
            {
                string chunkId = Encoding.ASCII.GetString(wavData, chunkOffset, 4);
                int chunkLength = BitConverter.ToInt32(wavData, chunkOffset + 4);
                int chunkDataOffset = chunkOffset + 8;
                if (chunkLength < 0 || chunkDataOffset + (long)chunkLength > wavData.Length)
                {
                    throw new InvalidOperationException("The custom TTS WAV contains an invalid chunk length.");
                }

                if (chunkId == "fmt " && chunkLength >= 16)
                {
                    format = BitConverter.ToUInt16(wavData, chunkDataOffset);
                    channels = BitConverter.ToUInt16(wavData, chunkDataOffset + 2);
                    sampleRate = BitConverter.ToInt32(wavData, chunkDataOffset + 4);
                    bitsPerSample = BitConverter.ToUInt16(wavData, chunkDataOffset + 14);

                    if (format == 0xFFFE && chunkLength >= 40)
                    {
                        format = BitConverter.ToUInt16(wavData, chunkDataOffset + 24);
                    }
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkDataOffset;
                    dataLength = chunkLength;
                }

                chunkOffset = chunkDataOffset + chunkLength + (chunkLength & 1);
            }

            if ((format != 1 && format != 3) || channels == 0 || sampleRate <= 0 ||
                bitsPerSample == 0 || dataOffset < 0 || dataLength == 0)
            {
                throw new InvalidOperationException(
                    $"Unsupported custom TTS WAV format: format={format}, channels={channels}, " +
                    $"sampleRate={sampleRate}, bitsPerSample={bitsPerSample}.");
            }

            int bytesPerSample = (bitsPerSample + 7) / 8;
            int frameSize = bytesPerSample * channels;
            if (frameSize <= 0 || dataLength < frameSize)
            {
                throw new InvalidOperationException("The custom TTS WAV does not contain complete audio frames.");
            }

            int sourceFrameCount = dataLength / frameSize;
            var monoSamples = new float[sourceFrameCount];
            for (int frame = 0; frame < sourceFrameCount; frame++)
            {
                float sampleSum = 0f;
                int frameOffset = dataOffset + frame * frameSize;
                for (int channel = 0; channel < channels; channel++)
                {
                    sampleSum += ReadWavSample(
                        wavData,
                        frameOffset + channel * bytesPerSample,
                        format,
                        bitsPerSample);
                }
                monoSamples[frame] = sampleSum / channels;
            }

            const int targetSampleRate = 24000;
            int targetFrameCount = Math.Max(1, (int)Math.Round(
                sourceFrameCount * (double)targetSampleRate / sampleRate));
            var pcmData = new byte[targetFrameCount * 2];
            for (int frame = 0; frame < targetFrameCount; frame++)
            {
                double sourcePosition = frame * (double)sampleRate / targetSampleRate;
                int firstFrame = Math.Min((int)sourcePosition, sourceFrameCount - 1);
                int secondFrame = Math.Min(firstFrame + 1, sourceFrameCount - 1);
                float fraction = (float)(sourcePosition - firstFrame);
                float sample = monoSamples[firstFrame] +
                    (monoSamples[secondFrame] - monoSamples[firstFrame]) * fraction;
                short pcmSample = (short)Math.Round(
                    Math.Max(-1f, Math.Min(1f, sample)) * 32767f);
                pcmData[frame * 2] = (byte)(pcmSample & 0xff);
                pcmData[frame * 2 + 1] = (byte)((pcmSample >> 8) & 0xff);
            }

            LogManager.Log(
                $"[TTS] Converted WAV: {sampleRate} Hz, {channels} channel(s), " +
                $"{bitsPerSample}-bit to {targetSampleRate} Hz mono PCM16.");
            return pcmData;
        }

        private static float ReadWavSample(byte[] data, int offset, ushort format, ushort bitsPerSample)
        {
            if (format == 3 && bitsPerSample == 32)
            {
                return BitConverter.ToSingle(data, offset);
            }

            if (format != 1)
            {
                throw new InvalidOperationException(
                    $"Unsupported custom TTS WAV encoding: format={format}, bitsPerSample={bitsPerSample}.");
            }

            switch (bitsPerSample)
            {
                case 8:
                    return (data[offset] - 128) / 128f;
                case 16:
                    return BitConverter.ToInt16(data, offset) / 32768f;
                case 24:
                    int sample24 = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
                    if ((sample24 & 0x800000) != 0) sample24 |= unchecked((int)0xff000000);
                    return sample24 / 8388608f;
                case 32:
                    return BitConverter.ToInt32(data, offset) / 2147483648f;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported custom TTS PCM bit depth: {bitsPerSample}.");
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
            LogManager.Log($"[TTS] Applying request template. Template: {template}, Text: {text}, Persona: {persona}, Voice: {voice}, Emotion: {emotion}, Mood: {mood}");
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
