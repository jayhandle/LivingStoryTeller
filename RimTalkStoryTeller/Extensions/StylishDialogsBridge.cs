using LivingStoryteller;
using RimWorld;
using Verse;

namespace Extension.LivingStoryTeller
{
    internal static class StylishDialogsBridge
    {
        private const string StylishDialogsPackageId = "costel.stylishdialogs";
        private static bool? _isAvailable;

        public static bool IsAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    _isAvailable =
                        ModLister.GetActiveModWithIdentifier(
                            StylishDialogsPackageId) != null;

                    if (_isAvailable.Value)
                    {
                        LogManager.Log(
                            "Stylish Dialogs detected. Narrations will use its styled dialog window.");
                    }
                }

                return _isAvailable.Value;
            }
        }
    }
}
