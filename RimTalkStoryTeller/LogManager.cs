using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryTeller
{
    public static class LogManager
    {
        public static void Log(string message)
        {
           // if (ModOptions.Settings == null || !ModOptions.Settings.DebugLogging) return;
            Verse.Log.Message($"[LivingStoryTeller] {message}");
        }

        internal static void Warning(string message)
        {
         //   if (ModOptions.Settings == null || !ModOptions.Settings.DebugLogging) return;
            Verse.Log.Warning($"[LivingStoryTeller] {message}");
        }
    }
}
