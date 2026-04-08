using LudeonTK;
using Verse;

namespace LivingStoryteller
{
    public static class DebugActions_TTS
    {
       // [DebugAction("Living Storyteller", "Test TTS Narration", allowedGameStates = AllowedGameStates.Playing)]
        public static void TestTTS()
        {
            StorytellerAIService.RequestNarration(
                "Test Event",
                "Debug",
                "This is a test of the storyteller voice.",
                "Colony: 3 colonists, 12000 wealth, day 5.",
                "Test Storyteller",
                "default_voice"
            );
        }
    }
}
