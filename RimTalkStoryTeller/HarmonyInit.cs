using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{

    [StaticConstructorOnStartup]
    public static class LivingStoryTellerStartup
    {
        static LivingStoryTellerStartup()
        {
            var harmony = new Harmony("JayHandle.LivingStoryTeller");

            try
            {
                var target = typeof(RimWorld.IncidentWorker).GetMethod(
                    "TryExecute",
                    BindingFlags.Instance | BindingFlags.Public);

                if (target == null)
                {
                    LogManager.Log(
                        "[LivingStoryTeller] Could not find TryExecute!");
                    return;
                }

                var postfix = typeof(Patch_IncidentWorker).GetMethod(
                    "Postfix",
                    BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                LogManager.Log(
                    "[LivingStoryTeller] Successfully patched: "
                    + target.Name);
            }
            catch (Exception ex)
            {
                LogManager.Error(
                    "[LivingStoryTeller] Patching failed: " + ex);
            }

            LogManager.Log("[LivingStoryTeller] Loaded successfully.");
        }
    }
}