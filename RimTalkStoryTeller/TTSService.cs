using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{
    public static class TTSService
    {
        private static readonly object audioLock = new object();
        private static byte[] pendingPcm;
        private static bool hasPendingClip = false;
        private static readonly HttpClient httpClient = new HttpClient();
        public static bool ProcessingAudio = false;

        // Called every frame from StorytellerAIService.ProcessPending()
        public static async Task ProcessPendingAudio()
        {
            byte[] pcm = null;

            //lock (audioLock)
            //{
           
                while (!hasPendingClip)
                {
                    // Wait until main thread has processed the pending clip
                    await Task.Delay(500);
                }
            
                LogManager.Log("[TTS] Has Pending Clip.");
                ProcessingAudio = false;
                pcm = pendingPcm;
                pendingPcm = null;
                hasPendingClip = false;
            //}

            if (pcm != null)
            {
                LogManager.Log("[TTS] Processing pending PCM data length = " + pcm.Length);
                var clip = PCM16ToAudioClip(pcm, 24000);
                if (clip != null)
                {
                    LogManager.Log("[TTS] Clip samples = " + clip.samples);
                    PlayClip(clip);
                }
                else
                {
                    Log.Warning("[TTS] Failed to create AudioClip from PCM.");
                }
            }
        }

        public static void RequestSpeech(string text, string voiceId)
        {
            LogManager.Log("[TTS] RequestSpeech called. Text length = " + text.Length + ", voiceId = " + voiceId);
            var settings = ModOptions.Settings;

            if (settings.apiKey.NullOrEmpty())
            {
                Log.Warning("[LivingStoryteller][TTS] No API key for TTS.");
                return;
            }

            ProcessingAudio = true;

            Task.Run(async () =>
            {
                try
                {
                    byte[] pcm = await CallTTSAPIAsync(settings.apiKey, voiceId, text);

                    if (pcm != null && pcm.Length > 0)
                    {
                        LogManager.Log("[TTS] Received PCM data length = " + pcm.Length);    
                        pendingPcm = pcm;
                        hasPendingClip = true;
                    }
                    else
                    {
                        Log.Warning("[LivingStoryteller][TTS] PCM data was null or empty.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[LivingStoryteller] TTS failed: " + ex);
                }

                ProcessingAudio = false;
            });
        }
        private static async Task<byte[]> CallTTSAPIAsync(string apiKey, string voiceId, string text)
        {
            var settings = ModOptions.Settings;

            if (settings.provider == 0)
            {
                // OPENAI – still fine to return AudioClip if WavUtility is main-thread safe,
                // but to keep things consistent, I'd also return raw bytes here ideally.
                // For now, leave as-is if it's working.
                throw new NotImplementedException("Focus on Google branch for now.");
            }
            else
            {
                string url =
                    "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key=" + apiKey;

                string googleVoice = ResolveGoogleVoice(voiceId);

                string json =
                    "{ \"contents\":[{\"parts\":[{\"text\": \" Speak with a refined British accent: " + Escape(text) + "\"}]}]," +
                    "\"generationConfig\": { \"responseModalities\":[\"AUDIO\"], \"speechConfig\": { \"voiceConfig\": { \"prebuiltVoiceConfig\": { \"voiceName\": \"" + googleVoice + "\" }}}}," +
                    "\"model\":\"gemini-2.5-flash-preview-tts\"" +
                    "}";

                LogManager.Log("[TTS] Using GOOGLE TTS endpoint.");
                LogManager.Log("[TTS] URL = " + url);
                LogManager.Log("[TTS] JSON = " + json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resp = await httpClient.PostAsync(url, content))
                {
                    resp.EnsureSuccessStatusCode();
                    string responseBody = await resp.Content.ReadAsStringAsync();
                    LogManager.Log("[TTS] responseBody status code = " + resp.StatusCode);

                    var pcmData = ExtractInlinePCM(responseBody);
                    return pcmData;
                }
            }
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
            LogManager.Log("[TTS] Extracted base64 PCM length = " + base64.Length + "substring:" + base64.Substring(0, 10));
            return Convert.FromBase64String(base64);
        }

        public static AudioClip PCM16ToAudioClip(byte[] pcmData, int sampleRate = 24000)
        {
            if (pcmData == null || pcmData.Length == 0)
                return null;

            int totalSamples = pcmData.Length / 2; // 16-bit = 2 bytes per sample
            float[] floatData = new float[totalSamples];

            // Convert PCM16 → float (-1 to 1)
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                floatData[i] = sample / 32768f;
            }

            // Create AudioClip
            AudioClip clip = AudioClip.Create(
                "GoogleTTS_Audio",
                totalSamples,
                1,              // mono
                sampleRate,
                false           // no streaming
            );

            clip.SetData(floatData, 0);
            return clip;
        }
        private static string ResolveGoogleVoice(string personaVoiceId)
        {
            switch (personaVoiceId)
            {
                case "cassandra_voice":
                    return "Zephyr"; // calm female
                case "randy_voice":
                    return "en-US-Neural2-D"; // energetic male
                case "phoebe_voice":
                    return "en-US-Neural2-E"; // soft female
                default:
                    return "en-US-Neural2-A"; // fallback
            }
        }

        private static string ResolveOpenAIVoice(string personaVoiceId)
        {
            switch (personaVoiceId)
            {
                case "cassandra_voice":
                    return "luna";
                case "randy_voice":
                    return "alloy";
                case "phoebe_voice":
                    return "sage";
                default:
                    return "alloy";
            }
        }

        private static void PlayClip(AudioClip clip)
        {
            if (clip == null)
            {
                Log.Warning("[LivingStoryteller][TTS] PlayClip called with null clip.");
                return;
            }

            Camera cam = Find.Camera;
            if (cam == null)
            {
                Log.Warning("[LivingStoryteller][TTS] No camera found for audio playback.");
                return;
            }

            var source = cam.gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = cam.gameObject.AddComponent<AudioSource>();
            }

            source.spatialBlend = 0f;
            source.volume = Prefs.VolumeGame;
            source.loop = false;

            source.clip = clip;
            source.Play();

            LogManager.Log("[LivingStoryteller][TTS] Audio playback started. Length = " + clip.length + "s");
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");
        }
    }
}