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
                    Log.Error(
                        "[LivingStoryTeller] Could not find TryExecute!");
                    return;
                }

                var postfix = typeof(Patch_IncidentWorker).GetMethod(
                    "Postfix",
                    BindingFlags.Static | BindingFlags.Public);

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                Log.Message(
                    "[LivingStoryTeller] Successfully patched: "
                    + target.Name);
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[LivingStoryTeller] Patching failed: " + ex);
            }

            Log.Message("[LivingStoryTeller] Loaded successfully.");
        }
    }
}