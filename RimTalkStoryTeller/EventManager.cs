//using HarmonyLib;
//using RimWorld;
//using System;
//using System.Linq;
//using Verse;
//using Verse.AI;

//namespace LivingStoryteller
//{
//    [StaticConstructorOnStartup]
//    public static class EventManager
//    {
//        static EventManager()
//        {            
//            LogManager.Log("[LivingStoryteller] EventManager initialized.");
//        }

//        public static void Dispatch(string eventType, object data = null)
//        {
//            try
//            {
//                LogManager.Log($"[EventManager]eventType: {eventType}");

//                OnGameEvent(eventType, data);
//            }
//            catch (Exception ex)
//            {
//                LogManager.Error($"[LivingStoryteller] Error dispatching event {eventType}: {ex}");
//            }
//        }

//        public static void OnGameEvent(string eventType, object data)
//        {
//            // You can route events here:
//            // - classify them
//            // - generate narration
//            // - pick persona
//            // - send to TTS
//            // - queue audio
//        }
//    }



//    // ============================================================
//    // 1. STORYTELLER INCIDENTS (raids, infestations, quests, etc.)
//    // ============================================================
//    [HarmonyPatch(typeof(Storyteller), "TryFire")]
//    public static class Patch_Storyteller_TryFire
//    {
//        static void Postfix(bool __result, FiringIncident fi)
//        {
//            if (!__result || fi == null) return;

//            EventManager.Dispatch("IncidentFired", new
//            {
//                incident = fi.def.defName,
//                parms = fi.parms
//            });
//        }
//    }

//    // ============================================================
//    // 2. GAME CONDITIONS (toxic fallout, eclipse, volcanic winter)
//    // ============================================================
//    [HarmonyPatch(typeof(GameConditionManager), "RegisterCondition")]
//    public static class Patch_GameCondition_Start
//    {
//        static void Postfix(GameCondition cond)
//        {
//            EventManager.Dispatch("GameConditionStarted", new
//            {
//                condition = cond.def.defName,
//                cond
//            });
//        }
//    }

//    [HarmonyPatch(typeof(GameConditionManager), "EndCondition")]
//    public static class Patch_GameCondition_End
//    {
//        static void Postfix(GameCondition cond)
//        {
//            EventManager.Dispatch("GameConditionEnded", new
//            {
//                condition = cond.def.defName,
//                cond
//            });
//        }
//    }

//    // ============================================================
//    // 3. PAWN LIFE EVENTS (death, birth, surgery, downed)
//    // ============================================================
//    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
//    public static class Patch_Pawn_Kill
//    {
//        static void Postfix(Pawn __instance)
//        {
//            EventManager.Dispatch("PawnDied", new
//            {
//                pawn = __instance,
//                name = __instance.Name?.ToStringShort,
//                faction = __instance.Faction?.Name
//            });
//        }
//    }

//    [HarmonyPatch(typeof(Pawn), "GiveBirth")]
//    public static class Patch_Pawn_GiveBirth
//    {
//        static void Postfix(Pawn __instance, Pawn baby)
//        {
//            EventManager.Dispatch("PawnBorn", new
//            {
//                mother = __instance,
//                baby
//            });
//        }
//    }

//    [HarmonyPatch(typeof(Recipe_Surgery), "ApplyOnPawn")]
//    public static class Patch_Surgery
//    {
//        static void Postfix(Pawn pawn, BodyPartRecord part)
//        {
//            EventManager.Dispatch("SurgeryPerformed", new
//            {
//                pawn,
//                part = part?.Label
//            });
//        }
//    }

//    // ============================================================
//    // 4. MENTAL BREAKS
//    // ============================================================
//    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")]
//    public static class Patch_MentalBreak
//    {
//        static void Postfix(Pawn ___pawn, MentalStateDef stateDef)
//        {
//            EventManager.Dispatch("MentalBreak", new
//            {
//                pawn = ___pawn,
//                state = stateDef.defName
//            });
//        }
//    }

//    // ============================================================
//    // 5. TALE RECORDER (marriage, social fights, bonding, etc.)
//    // ============================================================
//    [HarmonyPatch(typeof(TaleRecorder), nameof(TaleRecorder.RecordTale))]
//    public static class Patch_TaleRecorder_RecordTale
//    {
//        static void Postfix(TaleDef def, object[] args)
//        {
//            EventManager.Dispatch("TaleRecorded", new
//            {
//                tale = def.defName,
//                args
//            });
//        }
//    }
//}
