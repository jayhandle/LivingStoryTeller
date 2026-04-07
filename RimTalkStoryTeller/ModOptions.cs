using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{
    public class ModOptions : Mod
    {
        public static StorytellerSettings Settings;

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
            listing.Begin(inRect);

            // Provider toggle
            string providerLabel = Settings.provider == 0
                ? "OpenAI" : "Google AI Studio";
            if (listing.ButtonText("Provider: " + providerLabel))
            {
                Settings.provider = Settings.provider == 0 ? 1 : 0;
            }

            listing.Gap();
            listing.Label("API Key:");
            Settings.apiKey = listing.TextEntry(Settings.apiKey);

            listing.Gap();
            string defaultModel = Settings.provider == 0
                ? "gpt-4o-mini" : "gemini-2.0-flash";
            listing.Label("Model (blank = " + defaultModel + "):");
            Settings.modelName = listing.TextEntry(Settings.modelName);

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

            listing.End();
        }
    }

    [StaticConstructorOnStartup]
    public static class LivingStorytellerStartup
    {
        static LivingStorytellerStartup()
        {
            var harmony = new Harmony("Jessie.LivingStoryteller");

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
