using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryteller
{
    public class LivingStorytellerMapComponent : MapComponent
    {

        public LivingStorytellerMapComponent(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            if (!ModOptions.Settings.GameLoaded)
            {
                ModOptions.Settings.GameLoaded = true;
                StorytellerAIService.GreetPlayer(map);
            }
        }
    }
}
