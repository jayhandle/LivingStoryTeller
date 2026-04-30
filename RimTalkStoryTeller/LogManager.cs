using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryteller
{
    public static class LogManager
    {
        private static readonly string path = System.IO.Path.Combine(ModOptions.ModContent.RootDir, "Log") + "/log.txt";

        public static void Init() 
        {
            try
            {
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(ModOptions.ModContent.RootDir, "Log")))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(ModOptions.ModContent.RootDir, "Log"));
                }
                if (!System.IO.File.Exists(path))
                {
                    System.IO.File.Create(path).Dispose();
                }
                else
                {
                    System.IO.File.WriteAllText(path, $"[LivingStoryTeller] Log initialized at {DateTime.Now}\n");
                }
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[LivingStoryTeller] Failed to initialize log file: {ex.Message}");
            }
        }
        public static void Log(string message)
        {
            if (ModOptions.Settings == null || !ModOptions.Settings.DebugLogging) return;
            
            Verse.Log.Message($"[LivingStoryTeller] {message}");
                try
                {
                    System.IO.File.AppendAllText(path, $"[LivingStoryTeller] {DateTime.Now}: {message}\n");
                }
                catch (Exception ex)
                {
                    Verse.Log.Error($"[LivingStoryTeller] Failed to write to log file: {ex.Message}");
                }

        }

        internal static void Warning(string message)
        {
            if (ModOptions.Settings == null || !ModOptions.Settings.DebugLogging) return;
            
            Verse.Log.Warning($"[LivingStoryTeller] {message}");
            try
            {
                System.IO.File.AppendAllText(path, $"[LivingStoryTeller][WARNING] {DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[LivingStoryTeller] Failed to write to log file: {ex.Message}");
            }
        }
        internal static void Error(string message)
        {
            if (ModOptions.Settings == null || !ModOptions.Settings.DebugLogging) return;

            Verse.Log.Error($"[LivingStoryTeller] {message}");
            try
            {
                System.IO.File.AppendAllText(path, $"[LivingStoryTeller][ERROR] {DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                Verse.Log.Error($"[LivingStoryTeller] Failed to write to log file: {ex.Message}");
            }
        }
    }
}
