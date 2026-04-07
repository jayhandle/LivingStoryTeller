using Verse;

namespace LivingStoryteller
{
    public class StorytellerSettings : ModSettings
    {
        public string apiKey = "";
        public int provider = 0; // 0 = OpenAI, 1 = Google
        public string modelName = "";
        public float displayDuration = 15f;
        public float cooldownSeconds = 60f;

        public string GetEndpoint()
        {
            if (provider == 1)
            {
                return "https://generativelanguage.googleapis.com" +
                    "/v1beta/openai/chat/completions";
            }
            return "https://api.openai.com/v1/chat/completions";
        }

        public string GetModel()
        {
            if (!modelName.NullOrEmpty()) return modelName;
            if (provider == 1) return "gemini-2.5-flash";
            return "gpt-4o-mini";
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref provider, "provider", 0);
            Scribe_Values.Look(ref modelName, "modelName", "");
            Scribe_Values.Look(ref displayDuration,
                "displayDuration", 15f);
            Scribe_Values.Look(ref cooldownSeconds,
                "cooldownSeconds", 60f);
            base.ExposeData();
        }
    }
}
