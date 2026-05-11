using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LivingStoryteller
{
    public class MemoryManager : IExposable
    {
        public List<MemoryRecord> ShortTerm = new List<MemoryRecord>();
        public List<MemoryRecord> LongTerm = new List<MemoryRecord>();
        public List<StoryArc> ActiveArcs = new List<StoryArc>(); // this is for tracking ongoing story arcs like The Plaque Season, or The Era of Peace. I'll work on this in a future update, I have to think about how to implement it. Most likely ill have to use AI. 

        public void AddMemory(MemoryRecord mem)
        {
            LogManager.Log($"[MemoryManager] Adding memory: {mem.Type} | {mem.Description} | Significant: {mem.IsSignificant}");
            ShortTerm.Add(mem);

            if (mem.IsSignificant)
                LongTerm.Add(mem);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ShortTerm, "shortTerm", LookMode.Deep);
            Scribe_Collections.Look(ref LongTerm, "longTerm", LookMode.Deep);
            Scribe_Collections.Look(ref ActiveArcs, "activeArcs", LookMode.Deep);
        }

        public void Tick()
        {
            // Decay STM
            ShortTerm.RemoveAll(m => m.AgeTicks > 60000); // ~1 day

            // Decay LTM
            LongTerm.RemoveAll(m => m.AgeTicks > 1200000); // ~20 days

            foreach (var mem in ShortTerm)mem.Tick();
            foreach (var mem in LongTerm)mem.Tick();
            // Update arcs
            foreach (var arc in ActiveArcs)arc.Tick();
        }
    }

    public class MemoryRecord : IExposable
    {
        public string Type;            // "Death", "RaidVictory", "Breakup", etc.
        public string Description;     // "Colonist John died to infection"
        public int AgeTicks;           // incremented each tick
        public bool IsSignificant;     // determines if it goes to LTM

        public void ExposeData()
        {
            Scribe_Values.Look(ref Type, "Type");
            Scribe_Values.Look(ref Description, "Description");
            Scribe_Values.Look(ref AgeTicks, "AgeTicks");
            Scribe_Values.Look(ref IsSignificant, "IsSignificant");
        }

        public void Tick()
        {
            AgeTicks++;
        }
    }

    public class StoryArc : IExposable
    {
        public string Name;
        public string Description;
        public int AgeTicks;
        public bool IsActive;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "Name");
            Scribe_Values.Look(ref Description, "Description");
            Scribe_Values.Look(ref AgeTicks, "AgeTicks");
            Scribe_Values.Look(ref IsActive, "IsActive");
        }

        public void Tick()
        {
            AgeTicks ++; // per tick
            if (AgeTicks > 1200000) // ~20 days
                IsActive = false;
        }
    }
}
