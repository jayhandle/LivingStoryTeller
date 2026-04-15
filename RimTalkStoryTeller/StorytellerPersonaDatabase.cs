using System.Linq;
using Verse;

namespace LivingStoryteller
{
    [StaticConstructorOnStartup]
    public static class StorytellerPersonaDatabase
    {
        private static readonly StorytellerPersonaDef fallback;

        static StorytellerPersonaDatabase()
        {
            fallback = DefDatabase<StorytellerPersonaDef>
                .AllDefs
                .FirstOrDefault(d => d.storytellerDefName == "Fallback");

            foreach (var def in DefDatabase<StorytellerPersonaDef>.AllDefs)
            {
                Log.Message($"[LivingStoryteller] Loaded persona def: {def.defName} | Storyteller: {def.storytellerDefName} | VoiceId: {def.voiceId} | VoiceProviders: {(def.voiceProviders != null ? string.Join(", ", def.voiceProviders.Select(vp => $"{vp.name}:{vp.voice}")) : "none")} | PersonaText length: {def.personaText?.Length ?? 0}");
            }
        }

        public static StorytellerPersonaDef GetPersonaDef(string storytellerDefName)
        {
            return DefDatabase<StorytellerPersonaDef>
                .AllDefs
                .FirstOrDefault(d => d.storytellerDefName == storytellerDefName)
                ?? fallback;
        }

        public static string GetPersonaText(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.personaText ?? "";
        }

        public static string GetVoiceId(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.voiceId ?? "default_voice";
        }

        public static string GetVoice(string storytellerDefName, string providerName)
        {
            var voiceModel = GetPersonaDef(storytellerDefName)?.voiceProviders.FirstOrDefault(vm => vm.name == providerName);
            var voice = voiceModel?.voice ?? string.Empty;
            if(string.IsNullOrEmpty(voice))
                {
                Log.Warning($"No voice found for storyteller '{storytellerDefName}' and provider '{providerName}'. Using default voice.");
                voice = GetPersonaDef(storytellerDefName)?.voiceProviders.FirstOrDefault(vm => vm.name == "default")?.voice;
            }

            return voice;
        }
    }
}