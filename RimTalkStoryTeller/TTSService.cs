using NAudio.Wave;
using RimWorld;
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
    public class TTSResponseData
    {
        public TTSResponseData(byte[] data, string type = "") 
        {
            Data = data;
            DataType = type;
        }
        public byte[] Data;
        public string DataType;
    }
    public static class TTSService
    {
        private static readonly object audioLock = new object();
        private static TTSResponseData pendingPcm;
        private static bool hasPendingClip = false;
        private static readonly HttpClient httpClient = new HttpClient();
        public static bool ProcessingAudio = false;

        // Called every frame from StorytellerAIService.ProcessPending()
        public static async Task ProcessPendingAudio()
        {
            TTSResponseData pcm = null;

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
                LogManager.Log("[TTS] Processing pending PCM data length = " + pcm.Data.Length);
                if(pcm.DataType == "mpeg")
                {
                    LogManager.Log("[TTS] Converting MP3 to PCM.");
                    pcm.Data = Mp3ToPcm(pcm.Data);
                }

                LogManager.Log("[TTS] Converting PCM to AudioClip.");
                var clip = PCM16ToAudioClip(pcm.Data, 24000);
                if (clip != null)
                {
                    LogManager.Log("[TTS] Clip samples = " + clip.samples);
                    PlayClip(clip);
                }
                else
                {
                    LogManager.Warning("[TTS] Failed to create AudioClip from PCM.");
                }
            }
        }

        public static byte[] Mp3ToPcm(byte[] mpegData)
        {
            // 1. Wrap the input byte array in a MemoryStream
            using (var inputStream = new MemoryStream(mpegData))
            // 2. Pass the stream to an MP3/MPEG decoder
            using (var reader = new Mp3FileReader(inputStream))
            {
                // 3. Define the target 16-bit PCM format
                var targetFormat = new WaveFormat(reader.WaveFormat.SampleRate, 16, reader.WaveFormat.Channels);

                // 4. Use MediaFoundationResampler to convert the decoded bitstream into PCM16
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                {
                    resampler.ResamplerQuality = 60; // Highest quality conversion

                    // 5. Read from the resampler into a memory stream to capture the output bytes
                    using (var outputStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096]; // 4KB chunk buffer
                        int bytesRead;

                        while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outputStream.Write(buffer, 0, bytesRead);
                        }

                        // Return the raw PCM16 byte array
                        return outputStream.ToArray();
                    }
                }
            }
        }

        public static void RequestSpeech(string text, string PersonaDefName, string emotion, string mood)
        {
            LogManager.Log("[TTS] RequestSpeech called. Text length = " + text.Length + ", PersonaDefName = " + PersonaDefName);
            var settings = ModOptions.Settings;

            if (settings.ApiKey.NullOrEmpty())
            {
                LogManager.Warning("[LivingStoryteller][TTS] No API key for TTS.");
                return;
            }

            ProcessingAudio = true;

            Task.Run(async () =>
            {
                try
                {
                    TTSResponseData pcm = await CallTTSAPIAsync(settings.ApiKey, PersonaDefName, text, emotion, mood);

                    if (pcm != null && pcm.Data.Length > 0)
                    {
                        LogManager.Log("[TTS] Received PCM data length = " + pcm.Data.Length);    
                        pendingPcm = pcm;
                        hasPendingClip = true;
                    }
                    else
                    {
                        LogManager.Warning("[LivingStoryteller][TTS] PCM data was null or empty.");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("[LivingStoryteller] TTS failed: " + ex);
                }

                ProcessingAudio = false;
            });
        }
        private static async Task<TTSResponseData> CallTTSAPIAsync(string apiKey, string PersonaDefName, string text, string emotion, string mood)
        {
            var settings = ModOptions.Settings;
            string url = settings.TTSEndpoint;
            string voice = ResolveVoice(PersonaDefName, ModOptions.Settings.ProviderName.ToString());
            if (voice.NullOrEmpty())
            {
                LogManager.Warning("[TTS] No voice mapping found for PersonaDefName: " + PersonaDefName + " with provider: " + ModOptions.Settings.ProviderName);
                voice = ResolveVoice("FallbackPersona", ModOptions.Settings.ProviderName.ToString()); // default fallback
            }

            LogManager.Log("[TTS] Resolved voice for " + PersonaDefName + " is " + voice);

            string json = AIProviderFactory.JSONTTSRequest(Escape(text), PersonaDefName, voice, emotion, mood);

            LogManager.Log($"[TTS] Using {ModOptions.Settings.TTSProviderName} TTS endpoint.");
            LogManager.Log("[TTS] URL = " + url);
            LogManager.Log("[TTS] JSON = " + json);

            var responseBody = await AIProviderFactory.GetTTSResponse(json);
            return responseBody;
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
                "TTS_Audio",
                totalSamples,
                1,              // mono
                sampleRate,
                false           // no streaming
            );

            clip.SetData(floatData, 0);
            return clip;
        }

        private static string ResolveVoice(string PersonaDefName, string providerName)
        {
            var voice = StorytellerPersonaDatabase.GetVoice(PersonaDefName, providerName);

            return voice; 
        }

        private static void PlayClip(AudioClip clip)
        {
            if (clip == null)
            {
                LogManager.Warning("[LivingStoryteller][TTS] PlayClip called with null clip.");
                return;
            }

            Camera cam = Find.Camera;
            if (cam == null)
            {
                LogManager.Warning("[LivingStoryteller][TTS] No camera found for audio playback.");
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