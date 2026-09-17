using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Runtime;
using UnityEngine;
using Verse;
using Verse.Noise;
using static RimWorld.Dialog_StylingStation;
using static System.Net.WebRequestMethods;

namespace LivingStoryteller
{
    public class ModOptions : Mod
    {
        public static StorytellerSettings Settings;
        private Vector2 optionScrollPos;
        private Vector2 detailScrollPos;
        private const float tabHeight = 30f;
        public static ModContentPack ModContent;
        private StorytellerPersonaDef selectedPersonaDef;
        private enum Tab
        {
            General,
            Personas
        }
        private Tab currentTab = Tab.General;

        public ModOptions(ModContentPack content) : base(content)
        {
            Settings = GetSettings<StorytellerSettings>();
            ModContent = content;

        }

        public override string SettingsCategory()
        {
            return "The Living Storyteller";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("General", () => currentTab = Tab.General, () => currentTab == Tab.General),
                new TabRecord("Storyteller Personas", () => currentTab = Tab.Personas, () => currentTab == Tab.Personas)
            };


            var tabRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, tabHeight);
            TabDrawer.DrawTabs(tabRect, tabs);
            var contentRect = new Rect(inRect.x, tabRect.yMax, inRect.width, inRect.height - tabHeight );
            switch (currentTab)
            {
                case Tab.General:
                    GeneralTab(contentRect);
                    break;
                case Tab.Personas:
                    PersonaTab(contentRect);
                    break;
            }

            
            base.DoSettingsWindowContents(inRect);
        }

        private void GeneralTab(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            Widgets.BeginScrollView(inRect, ref optionScrollPos, new Rect(0f, 0f, inRect.width, inRect.height + 1400), true);
            listing.Begin(new Rect(0, 0, inRect.width - 25, inRect.height + 1400));
            listing.CheckboxLabeled("Enable Storyteller Mod", ref Settings.EnableStoryTeller);
            listing.CheckboxLabeled("Enable Debug Logging", ref Settings.DebugLogging);
            listing.Gap();

            // Provider toggle
            string providerLabel = ConvertProviderToLabel(Settings.ProviderName);

            listing.Label("AI Provider:");
            if (listing.ButtonText(ConvertProviderToLabel(Settings.ProviderName)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (StorytellerSettings.AIProvider provider in Enum.GetValues(typeof(StorytellerSettings.AIProvider)))
                {
                    options.Add(new FloatMenuOption(ConvertProviderToLabel(provider), () =>
                    {
                        Settings.ProviderName = provider;
                        providerLabel = ConvertProviderToLabel(Settings.ProviderName);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Label("AI TTS Provider:");
            if (listing.ButtonText(ConvertProviderToLabel(Settings.TTSProviderName)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (StorytellerSettings.AIProvider provider in Enum.GetValues(typeof(StorytellerSettings.AIProvider)))
                {
                    options.Add(new FloatMenuOption(ConvertProviderToLabel(provider), () =>
                    {
                        Settings.TTSProviderName = provider;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (listing.ButtonText("Reset Defaults for: " + providerLabel))
            {
                providerDefaults();
            }

            listing.Gap();
            listing.Label("Model (blank = " + Settings.ModelName + "):");
            Settings.ModelName = listing.TextEntry(Settings.ModelName);

            listing.Gap();
            listing.Label("Endpoint:");
            Settings.Endpoint = listing.TextEntry(Settings.Endpoint);

            listing.Gap();
            listing.Label("API Key:");
            Settings.ApiKey = listing.TextEntry(Settings.ApiKey);

            listing.Gap();
            listing.CheckboxLabeled("Enable TTS", ref Settings.TTSEnabled);

            if (Settings.TTSEnabled)
            {
                listing.GapLine();

                listing.Gap();
                listing.Label("TTS Model (blank = " + Settings.TTSModelName + "):");
                Settings.TTSModelName = listing.TextEntry(Settings.TTSModelName);

                listing.Gap();
                listing.Label("TTS Endpoint:");
                Settings.TTSEndpoint = listing.TextEntry(Settings.TTSEndpoint);

                listing.Gap();
                listing.Label("TTS API Key:");
                Settings.TTSApiKey = listing.TextEntry(string.IsNullOrWhiteSpace(Settings.TTSApiKey) ? Settings.ApiKey : Settings.TTSApiKey);

                if (Settings.TTSProviderName == StorytellerSettings.AIProvider.custom)
                {
                    DrawCustomTTSSettings(listing);
                }
            }

            listing.GapLine();

            listing.Gap();
            listing.Label(
                "Display Duration: " +
                Settings.displayDuration.ToString("F0") + " seconds");
            Settings.displayDuration = listing.Slider(
                Settings.displayDuration, 5f, 60f);

            listing.Gap();
            listing.Label(
                "Cooldown Between Narrations: " +
                Settings.cooldownSeconds.ToString("F0") + " seconds");
            Settings.cooldownSeconds = listing.Slider(
                Settings.cooldownSeconds, 10f, 300f);

            listing.Gap();
            listing.CheckboxLabeled("Skip events during cooldown", ref Settings.SkipEventsDuringCooldown);

            listing.Gap();
            listing.CheckboxLabeled("Enable EchoTales integration (daily tale commentary)", ref Settings.EnableEchoTalesIntegration);

            if (Settings.EnableEchoTalesIntegration)
            {
                listing.Gap();
                listing.CheckboxLabeled("EchoTales: read every new entry (not just once per day)", ref Settings.EchoTalesReadEveryNewEntry);
            }

            listing.Gap();
            listing.CheckboxLabeled("Silent Raids: stay quiet about raids (remember them only)", ref Settings.EnableSilentRaidsIntegration);

            listing.Gap();
            listing.Label("Storyteller Memory Capacity: " + Settings.MemoryCapacity + " events");
            Settings.MemoryCapacity = (int)listing.Slider(Settings.MemoryCapacity, 5f, 100f);

            listing.GapLine();
            listing.Gap();
            listing.Label("This is the general persona. If you do not know what you are doing, do not change it!");
            Settings.PersonaText = listing.TextEntry(Settings.PersonaText, lineCount: 6);
            listing.GapLine();
            listing.Gap();
            //listing.Label("Storyteller Configuration");
            //for (int i = 0; i < Settings.Storytellers.Count; i++)
            //{
            //    string? storyteller = Settings.Storytellers[i];

            //    listing.ButtonText(storyteller);

            //    //string currentPersona = Settings.StorytellerPersonas.ContainsKey(storyteller) ? Settings.StorytellerPersonas[storyteller].personaText : "";
            //    //string newPersona = listing.TextEntry(currentPersona, lineCount: 6);
            //    //if (newPersona != currentPersona)
            //    //{
            //    //    if (!Settings.StorytellerPersonas.ContainsKey(storyteller))
            //    //    {
            //    //        Settings.StorytellerPersonas[storyteller] = new StorytellerPersonaDef { storytellerDefName = storyteller };
            //    //    }
            //    //    Settings.StorytellerPersonas[storyteller].personaText = newPersona;
            //    //}
            //}
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawCustomTTSSettings(Listing_Standard listing)
        {
            listing.GapLine();
            listing.Label("Custom TTS Response Mode:");
            if (listing.ButtonText(CustomTTSModeLabel(Settings.CustomTTSMode)))
            {
                var options = new List<FloatMenuOption>();
                foreach (StorytellerSettings.CustomTTSResponseMode mode in Enum.GetValues(typeof(StorytellerSettings.CustomTTSResponseMode)))
                {
                    var selectedMode = mode;
                    options.Add(new FloatMenuOption(CustomTTSModeLabel(mode), () => Settings.CustomTTSMode = selectedMode));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (listing.ButtonText("Load Local JSON Download Preset"))
            {
                Settings.TTSEndpoint = "http://10.0.0.193:8000/synthesize";
                Settings.CustomTTSMode = StorytellerSettings.CustomTTSResponseMode.json_download;
                Settings.CustomTTSAudioType = StorytellerSettings.CustomTTSAudioFormat.wav;
                Settings.CustomTTSRequestTemplate = "{\"speaker_name\":\"{voice}\",\"language\":\"{language}\",\"text\":\"{text}\"}";
                Settings.CustomTTSLanguage = "en";
                Settings.CustomTTSContentType = "application/json";
                Settings.CustomTTSHeaderName = "X-API-Key";
                Settings.CustomTTSHeaderPrefix = "";
                Settings.CustomTTSDownloadPathField = "download_path";
                Settings.CustomTTSDownloadUrlTemplate = "";
            }

            if (Settings.CustomTTSMode == StorytellerSettings.CustomTTSResponseMode.legacy)
            {
                return;
            }

            listing.Label("Audio Format:");
            if (listing.ButtonText(CustomTTSAudioFormatLabel(Settings.CustomTTSAudioType)))
            {
                var options = new List<FloatMenuOption>();
                foreach (StorytellerSettings.CustomTTSAudioFormat format in Enum.GetValues(typeof(StorytellerSettings.CustomTTSAudioFormat)))
                {
                    var selectedFormat = format;
                    options.Add(new FloatMenuOption(CustomTTSAudioFormatLabel(format), () => Settings.CustomTTSAudioType = selectedFormat));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Label("Request Body Template:");
            Settings.CustomTTSRequestTemplate = listing.TextEntry(Settings.CustomTTSRequestTemplate, lineCount: 4);

            listing.Label("Language ({language}):");
            Settings.CustomTTSLanguage = listing.TextEntry(Settings.CustomTTSLanguage);

            listing.Label("Content Type:");
            Settings.CustomTTSContentType = listing.TextEntry(Settings.CustomTTSContentType);

            listing.Label("API Key Header Name:");
            Settings.CustomTTSHeaderName = listing.TextEntry(Settings.CustomTTSHeaderName);

            listing.Label("API Key Header Prefix:");
            Settings.CustomTTSHeaderPrefix = listing.TextEntry(Settings.CustomTTSHeaderPrefix);

            if (Settings.CustomTTSMode == StorytellerSettings.CustomTTSResponseMode.json_download)
            {
                listing.Label("Download Path JSON Field:");
                Settings.CustomTTSDownloadPathField = listing.TextEntry(Settings.CustomTTSDownloadPathField);

                listing.Label("Download URL Template (optional):");
                Settings.CustomTTSDownloadUrlTemplate = listing.TextEntry(Settings.CustomTTSDownloadUrlTemplate);
            }
        }

        private static string CustomTTSModeLabel(StorytellerSettings.CustomTTSResponseMode mode)
        {
            switch (mode)
            {
                case StorytellerSettings.CustomTTSResponseMode.direct_audio:
                    return "Direct Audio Response";
                case StorytellerSettings.CustomTTSResponseMode.json_download:
                    return "JSON Then Download";
                default:
                    return "Legacy (Unchanged)";
            }
        }

        private static string CustomTTSAudioFormatLabel(StorytellerSettings.CustomTTSAudioFormat format)
        {
            switch (format)
            {
                case StorytellerSettings.CustomTTSAudioFormat.wav:
                    return "WAV";
                case StorytellerSettings.CustomTTSAudioFormat.mp3:
                    return "MP3";
                default:
                    return "PCM16, 24 kHz, Mono";
            }
        }

        private void PersonaTab(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(inRect.x, inRect.y, inRect.width, inRect.height + 800));
            listing.Label("Storyteller Configuration");
            Rect boxRect = new Rect(inRect.x, inRect.y - 70, inRect.width, inRect.height - 100);

            Widgets.DrawBox(boxRect);
            Rect innerBoxRect = boxRect.ContractedBy(10f);
            float leftColumnWidth = innerBoxRect.width * 0.4f;

            Rect listRect = new Rect(innerBoxRect.x, innerBoxRect.y + 4f, leftColumnWidth, innerBoxRect.height);
            Rect detailRect = new Rect(listRect.xMax + 10f, innerBoxRect.y, innerBoxRect.width - leftColumnWidth - 10f, innerBoxRect.height);

            Widgets.DrawLineVertical(listRect.xMax + 5f, listRect.y, listRect.height);
            DrawPersonaLeftColumn(listRect);
            DrawStorytellerDetails(detailRect);
            listing.End();
        }

        private void DrawPersonaLeftColumn(Rect inRect) {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(inRect.x, inRect.y, inRect.width - 25, inRect.height));
            for (int i = 0; i < Settings.Storytellers.Count(); i++)
            {
                string? storyteller = Settings.Storytellers[i];
                if (listing.ButtonText(storyteller))
                {
                    selectedPersonaDef = StorytellerPersonaDatabase.GetPersonaDef(storyteller);
                }
            }

            listing.End();
        }

        private void DrawStorytellerDetails(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            Widgets.BeginScrollView(inRect, ref detailScrollPos, new Rect(0f, 0f, 0, inRect.height + 800), true);

            listing.Begin(new Rect(0, inRect.y - 30, inRect.width - 25, inRect.height + 800));
            if(selectedPersonaDef != null)
            {
                listing.Label("Storyteller: " + selectedPersonaDef.storytellerDefName);
                Texture2D portrait = GetStorytellerPortrait(selectedPersonaDef.storytellerDefName);
                listing.ButtonImage(portrait,64,64);
                listing.Gap();
                listing.Label("Persona:");
                selectedPersonaDef.personaText = listing.TextEntry(selectedPersonaDef.personaText, lineCount: 10);
                listing.Gap();
                listing.Label("Emotions:");
                listing.GapLine();
                listing.Label("Neutral:");
                selectedPersonaDef.emotionModifiers.neutral = listing.TextEntry(selectedPersonaDef.emotionModifiers.neutral, lineCount: 1);
                listing.Label("Tense:");
                selectedPersonaDef.emotionModifiers.tense = listing.TextEntry(selectedPersonaDef.emotionModifiers.tense, lineCount: 1);
                listing.Label("Chaotic:");
                selectedPersonaDef.emotionModifiers.chaotic = listing.TextEntry(selectedPersonaDef.emotionModifiers.chaotic, lineCount: 1);
                listing.Label("Somber:");
                selectedPersonaDef.emotionModifiers.somber = listing.TextEntry(selectedPersonaDef.emotionModifiers.somber, lineCount: 1);
                listing.Gap();
                listing.Label("Moods:");
                listing.GapLine();
                listing.Label("Neutral:");
                selectedPersonaDef.moodModifiers.neutral = listing.TextEntry(selectedPersonaDef.moodModifiers.neutral, lineCount: 1);
                listing.Label("Anxious:");
                selectedPersonaDef.moodModifiers.anxious = listing.TextEntry(selectedPersonaDef.moodModifiers.anxious, lineCount: 1);
                listing.Label("Chaotic:");
                selectedPersonaDef.moodModifiers.chaotic = listing.TextEntry(selectedPersonaDef.moodModifiers.chaotic, lineCount: 1);
                listing.Label("Somber:");
                selectedPersonaDef.moodModifiers.somber = listing.TextEntry(selectedPersonaDef.moodModifiers.somber, lineCount: 1);
                listing.Label("Confident:");
                selectedPersonaDef.moodModifiers.confident = listing.TextEntry(selectedPersonaDef.moodModifiers.confident, lineCount: 1);
                listing.Gap();
                listing.Label("TTS Configurations");
                listing.GapLine();
                listing.Label("Voice Providers:");
                for (int i = 0; i < selectedPersonaDef.voiceProviders.Count; i++)
                {
                    VoiceProvider vp = selectedPersonaDef.voiceProviders[i];
                    listing.Label(vp.name + ": " + vp.voice);
                    vp.voice = listing.TextEntry(vp.voice, lineCount: 1);
                }
                listing.Gap();
                listing.Label("Gender: (optional, for TTS)");
                selectedPersonaDef.gender = listing.TextEntry(selectedPersonaDef.gender ?? "other", lineCount: 1);
                //listing.Label("Voice ID: (optional, for TTS)");
                //selectedPersonaDef.voiceId = listing.TextEntry(selectedPersonaDef.voiceId);
                listing.Gap();
                listing.Label("Accent/Style: (optional, for TTS)");
                selectedPersonaDef.accent = listing.TextEntry(selectedPersonaDef.accent);
            }

            listing .Gap();
            if (listing.ButtonText("Save Changes"))
            {
                StorytellerPersonaDatabase.SaveToXml();
            }
            
            listing.End();
            Widgets.EndScrollView();
        }


        private static Texture2D GetStorytellerPortrait(string storytellerDefName)
        {
            var def = DefDatabase<StorytellerDef>.GetNamedSilentFail(storytellerDefName);
            if (def == null) 
            { 
                LogManager.Warning("[LivingStoryteller] Could not find storyteller def for portrait.");
                return null; 
            }

            if (def.portraitTinyTex != null)
                return def.portraitTinyTex;

            if (def.portraitLargeTex != null)
                return def.portraitLargeTex;

            LogManager.Warning("[LivingStoryteller] Could not find portrait for storyteller: " + def.defName);
            return null;
        }

        private static void providerDefaults()
        {
            switch (Settings.ProviderName)
            {
                case StorytellerSettings.AIProvider.open_ai:
                    Settings.Endpoint = "https://api.openai.com/v1/chat/completions";
                    Settings.TTSModelName = "gpt-4o-mini-tts";
                    Settings.TTSEndpoint = "https://api.openai.com/v1/audio/speech";
                    Settings.ModelName = "gpt-4o-mini";
                    Settings.ProviderName = StorytellerSettings.AIProvider.open_ai;
                    Settings.TTSProviderName = StorytellerSettings.AIProvider.open_ai;
                    break;
                case StorytellerSettings.AIProvider.player2:
                    Settings.Endpoint = "https://api.player2.game/v1/chat/completions";
                    Settings.TTSModelName = "player2";
                    Settings.TTSEndpoint = "https://api.player2.game/v1/tts/speak";
                    Settings.ModelName = "player2";
                    Settings.ProviderName = StorytellerSettings.AIProvider.player2; 
                    Settings.TTSProviderName = StorytellerSettings.AIProvider.player2;
                    break;
                case StorytellerSettings.AIProvider.novel_ai:
                    Settings.Endpoint = "https://text.novelai.net/ai/generate";
                    Settings.TTSModelName = "kayra-v1";
                    Settings.TTSEndpoint = "https://api.novelai.net/ai/generate-voice";
                    Settings.ModelName = "kayra-v1";
                    Settings.ProviderName = StorytellerSettings.AIProvider.novel_ai;
                    Settings.TTSProviderName = StorytellerSettings.AIProvider.novel_ai;
                    break;
                default:
                    Settings.Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
                    Settings.TTSModelName = "gemini-2.5-flash-preview-tts";
                    Settings.TTSEndpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
                    Settings.ModelName = "gemini-2.5-flash";
                    Settings.ProviderName = StorytellerSettings.AIProvider.google;
                    Settings.TTSProviderName = StorytellerSettings.AIProvider.google;
                    break;
            }
        }

        private string ConvertProviderToLabel(StorytellerSettings.AIProvider provider)
        {
            switch (provider)
            {
                case StorytellerSettings.AIProvider.open_ai:
                    return "OpenAI";
                case StorytellerSettings.AIProvider.custom:
                    return "Custom";
                    case StorytellerSettings.AIProvider.player2:
                        return "Player2";
                    case StorytellerSettings.AIProvider.novel_ai:
                        return "NovelAI";
                default:
                    return "Google AI Studio";
            }
        }
    }
}
