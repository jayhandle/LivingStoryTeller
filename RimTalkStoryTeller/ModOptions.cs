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
            Widgets.BeginScrollView(inRect, ref optionScrollPos, new Rect(0f, 0f, inRect.width, inRect.height + 800), true);
            listing.Begin(new Rect(0, 0, inRect.width - 25, inRect.height + 800));

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
            if (listing.ButtonText(ConvertProviderToLabel(Settings.ProviderName)))
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
                Settings.TTSApiKey = listing.TextEntry(Settings.TTSApiKey ?? Settings.ApiKey);

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
                    break;
                case StorytellerSettings.AIProvider.player2:
                    Settings.Endpoint = "https://api.player2.game/v1/chat/completions";
                    Settings.TTSModelName = "player2";
                    Settings.TTSEndpoint = "https://api.player2.game/v1/tts/speak";
                    Settings.ModelName = "player2";
                    break;
                default:
                    Settings.Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                    Settings.TTSModelName = "gemini-2.5-flash-preview-tts";
                    Settings.TTSEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key=";
                    Settings.ModelName = "gemini-2.5-flash";
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
                default:
                    return "Google AI Studio";
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class LivingStorytellerStartup
    {
        static LivingStorytellerStartup()
        {
            var harmony = new Harmony("JayHandle.LivingStoryteller");
            LogManager.Init();

            try
            {
                // Patch IncidentWorker.TryExecute
                var target = typeof(RimWorld.IncidentWorker).GetMethod(
                    "TryExecute",
                    BindingFlags.Instance | BindingFlags.Public);

                if (target == null)
                {
                    Log.Error(
                        "[LivingStoryteller] Could not find TryExecute!");
                    return;
                }

                var postfix = typeof(Patch_IncidentWorker).GetMethod(
                    "Postfix",
                    BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(target,
                    postfix: new HarmonyMethod(postfix));

                Log.Message(
                    "[LivingStoryteller] Successfully patched: "
                    + target.Name);

                // Patch UIRoot_Play.UIRootUpdate for safe UI queue
                var uiTarget = typeof(UIRoot_Play).GetMethod(
                    "UIRootUpdate",
                    BindingFlags.Instance | BindingFlags.Public);

                if (uiTarget != null)
                {
                    var uiPostfix =
                        typeof(Patch_UIRootUpdate).GetMethod(
                            "Postfix",
                            BindingFlags.Static |
                            BindingFlags.Public);

                    harmony.Patch(uiTarget,
                        postfix: new HarmonyMethod(uiPostfix));

                    Log.Message(
                        "[LivingStoryteller] UI update patched.");
                }
                else
                {
                    Log.Error(
                        "[LivingStoryteller] " +
                        "Could not find UIRootUpdate!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[LivingStoryteller] Patching failed: " + ex);
            }

            Log.Message(
                "[LivingStoryteller] The storyteller awakens.");
        }
    }

    public static class Patch_UIRootUpdate
    {
        public static void Postfix()
        {
            StorytellerAIService.ProcessPending();
        }
    }
}
