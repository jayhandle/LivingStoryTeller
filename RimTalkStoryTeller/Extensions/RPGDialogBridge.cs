using RimWorld;
using Verse;

namespace Extension.LivingStoryTeller
{
    public static class RPGDialogBridge
    {
        private static bool? _isAvailable;

        public static bool IsAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    _isAvailable =
                        ModLister.GetActiveModWithIdentifier(
                            "esvn.RPGDialog") != null;

                    if (_isAvailable.Value)
                    {
                        Log.Message(
                            "[LivingStoryteller] RPG Dialog " +
                            "detected. Narrations will use " +
                            "RPG dialog window.");
                    }
                }
                return _isAvailable.Value;
            }
        }

        public static void ShowNarration(
            string storytellerName, string text)
        {
            // Close any existing storyteller dialog
            var existing = Find.WindowStack?
                .WindowOfType<Dialog_NodeTree>();
            if (existing != null)
            {
                existing.Close(false);
            }

            // Build a simple dialog node with the narration
            DiaNode node = new DiaNode(text);

            DiaOption okOption = new DiaOption("OK");
            okOption.resolveTree = true;
            node.options.Add(okOption);

            // Open vanilla Dialog_NodeTree — RPG Dialog's
            // Harmony patch on DoWindowContents will
            // automatically render it in RPG style.
            // NarratorSelector falls back to the current
            // storyteller's portrait when no pawn is speaking.
            Find.WindowStack.Add(
                new Dialog_NodeTree(node));
        }
    }
}
