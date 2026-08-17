using RimWorld;
using System.Linq;
using Verse;

namespace LivingStoryteller
{
    /// <summary>
    /// When the "Silent Raids" mod is active the storyteller must not announce raids,
    /// otherwise it gives away the ambush. The raid is still recorded in memory so the
    /// storyteller can reference it later.
    /// </summary>
    internal static class SilentRaidsIntegration
    {
        private static bool? _isAvailable;
        private static bool loggedSuppressionDisabled;

        public static bool IsAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    _isAvailable = DetectMod();
                    if (_isAvailable.Value)
                    {
                        LogManager.Log("[SilentRaids] Silent Raids detected. Raid narration will be suppressed.");
                    }
                }
                return _isAvailable.Value;
            }
        }

        private static bool DetectMod()
        {
            var active = ModsConfig.ActiveModsInLoadOrder;
            if (active == null) return false;

            foreach (var mod in active)
            {
                if (mod == null) continue;
                if (Normalize(mod.PackageId).Contains("silentraid") ||
                    Normalize(mod.Name).Contains("silentraid"))
                {
                    return true;
                }
            }
            return false;
        }

        private static string Normalize(string value)
        {
            if (value.NullOrEmpty()) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// True when the incident is a raid that should stay silent.
        /// </summary>
        public static bool ShouldSuppressNarration(IncidentDef incident, IncidentWorker worker)
        {
            if (incident == null) return false;

            if (!ModOptions.Settings.EnableSilentRaidsIntegration)
            {
                if (!loggedSuppressionDisabled)
                {
                    LogManager.Log("[SilentRaids] Suppression disabled in mod settings.");
                    loggedSuppressionDisabled = true;
                }
                return false;
            }
            loggedSuppressionDisabled = false;

            if (!IsAvailable) return false;

            if (!IsRaid(incident, worker)) return false;

            LogManager.Log($"[SilentRaids] Suppressing narration for raid incident '{incident.defName}'. Stored in memory only.");
            return true;
        }

        private static bool IsRaid(IncidentDef incident, IncidentWorker worker)
        {
            string workerType = worker?.GetType().Name ?? incident.workerClass?.Name ?? string.Empty;
            return workerType.IndexOf("Raid", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   incident.defName.IndexOf("Raid", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
