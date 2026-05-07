using Google.GenAI;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LivingStoryteller
{

    [StaticConstructorOnStartup]
    public static class LivingStoryTellerStartup
    {
        static LivingStoryTellerStartup()
        {
            var harmony = new Harmony("JayHandle.LivingStoryteller");
            LogManager.Init();
            try
            {

                patcher("TryExecute", typeof(RimWorld.IncidentWorker).GetMethod("TryExecute", BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_IncidentWorker).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("RecordTale", typeof(RimWorld.TaleRecorder).GetMethod("RecordTale", BindingFlags.Static | BindingFlags.Public),
                    typeof(Patch_TaleRecorder_RecordTale).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("TryFire", typeof(RimWorld.Storyteller).GetMethod("TryFire", BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_Storyteller_FiringIncident).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("RegisterCondition", typeof(GameConditionManager).GetMethod("RegisterCondition",BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_GameCondition_Start).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("Kill", typeof(Pawn).GetMethod("Kill", BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_Pawn_Kill).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("MakeDowned", typeof(Pawn_HealthTracker).GetMethod("CheckForStateChange", BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_Pawn_Downed).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("TryStartMentalState", typeof(MentalStateHandler).GetMethod("TryStartMentalState",BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_MentalState_Start).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                patcher("AddDirect", typeof(HediffSet).GetMethod("AddDirect",BindingFlags.Instance | BindingFlags.Public),
                    typeof(Patch_Hediff_Added).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public),
                    ref harmony);
                // Patch UIRoot_Play.UIRootUpdate for safe UI queue
                var uiTarget = typeof(UIRoot_Play).GetMethod("UIRootUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (uiTarget != null)
                {
                    var uiPostfix = typeof(Patch_UIRootUpdate).GetMethod( "Postfix", BindingFlags.Static | BindingFlags.Public);
                    harmony.Patch(uiTarget, postfix: new HarmonyMethod(uiPostfix));
                    Log.Message("[LivingStoryteller] UI update patched.");
                }
                else
                {
                    Log.Error( "[LivingStoryteller] " + "Could not find UIRootUpdate!");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[LivingStoryteller] Patching failed: " + ex);
            }

            Log.Message("[LivingStoryteller] The storyteller awakens.");
        }

        private static void patcher(string name, MethodInfo? target, MethodInfo postfix, ref Harmony harmony) 
        {
            if (target == null)
            {
                LogManager.Error($"[LivingStoryteller] Could not find {name}!");
            }
            else
            {
                try
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    LogManager.Log("[LivingStoryteller] Successfully patched: " + target.Name);
                }
                catch (Exception ex)
                {
                    LogManager.Error($"[LivingStoryteller] Failed to patch {name}: {ex}");
                }
            }    
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