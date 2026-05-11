using LivingStoryteller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryTeller
{
    public class LivingStorytellerTicksComponent : GameComponent
    {
        public static MemoryManager MemoryManager = new MemoryManager();
        public static bool Initialized = false;
        public LivingStorytellerTicksComponent(Game game) {
            if (Initialized) return; //loads only once
            Initialized = true;
            Log.Message("[LivingStoryteller] LivingStorytellerTicksComponent initialized.");
        }
        
        public override void GameComponentTick()
        {
            // Tick memory system every 60 ticks (~1 seconds)
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                MemoryManager.Tick();
            }
        }

        public override void ExposeData()
        {
            // Save/load memory system
            Scribe_Deep.Look(ref MemoryManager, "LivingStorytellerMemory");
        }
    }
}
