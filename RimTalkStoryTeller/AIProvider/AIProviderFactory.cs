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
                case "google":
                    return new GoogleProvider();
                case "open_ai":
                    return new OpenAIProvider();
            }

            return null;
        }
    }
}
