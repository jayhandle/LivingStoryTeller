using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LivingStoryteller
{
    internal static class AIProviderFactory
    {
        public static IAIProvider CreateAIProvider()
        {
            if (ModOptions.Settings == null) return null;
            switch (ModOptions.Settings.ProviderName)
            {
                case StorytellerSettings.AIProvider.google:
                    return new GoogleProvider();
                case StorytellerSettings.AIProvider.open_ai:
                    return new OpenAIProvider();
            }

            return null;
        }
    }
}
