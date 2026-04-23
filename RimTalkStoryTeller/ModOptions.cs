using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using UnityEngine;
using Verse;
using static System.Net.WebRequestMethods;

namespace LivingStoryteller
{
    public class ModOptions : Mod
    {
        public static StorytellerSettings Settings;
        private Vector2 optionScrollPos;

        public ModOptions(ModContentPack content) : base(content)
        {
            Settings = GetSettings<StorytellerSettings>();
        }

        public override string SettingsCategory()
        {
            return "The Living Storyteller";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            Widgets.BeginScrollView(inRect, ref optionScrollPos, new Rect(0f, 0f, inRect.width, inRect.height + 400), true);
            listing.Begin(new Rect(0, 0, inRect.width - 25, inRect.height + 300));

            listing.CheckboxLabeled( "Enable Debug Logging",ref Settings.DebugLogging);
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


            if (listing.ButtonText("Reset Defaults for: " + providerLabel))
            {
                switch(Settings.ProviderName)
                {
                    case StorytellerSettings.AIProvider.open_ai:
                        Settings.Endpoint = "https://api.openai.com/v1/chat/completions";
                        Settings.TTSModelName = "gpt-4o-mini-tts";
                        Settings.TTSEndpoint = "https://api.openai.com/v1/audio/speech"; 
                        Settings.ModelName = "gpt-4o-mini";
                        break;
                    default:
                        Settings.Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
                        Settings.TTSModelName = "gemini-2.5-flash-preview-tts";
                        Settings.TTSEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key="; 
                        Settings.ModelName = "gemini-2.5-flash";
                        break;
                }
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
                Settings.Endpoint = listing.TextEntry(Settings.TTSEndpoint);

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
            
            listing.Gap();
            listing.GapLine();
            listing.Gap();
            listing.Label( "This is the general persona. If you do not know what you are doing, do not change it!");
            Settings.PersonaText = listing.TextEntry(Settings.PersonaText, lineCount: 6);
            listing.End();
            Widgets.EndScrollView();
        }

        private string ConvertProviderToLabel(StorytellerSettings.AIProvider provider)
        {
            switch (provider)
            {
                case StorytellerSettings.AIProvider.open_ai:
                    return "OpenAI";
                case StorytellerSettings.AIProvider.custom:
                    return "Custom";
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
