using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace PokeWorld
{
    internal class FloatMenuOptionProvider_PW_Fishing  : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            Thing fishingRod = null;
            foreach (var thing in pawn.EquippedWornOrInventoryThings)
                if (thing.TryGetComp<CompFishingRod>() != null)
                {
                    fishingRod = thing;
                    break;
                }
            if (fishingRod == null)
                return null;
            IntVec3 cell = context.ClickedCell;
            var targetTerrain = cell.GetTerrain(pawn.Map);
            if (!FishingUtility.IsFishingTerrain(targetTerrain)) return null;
            if (!pawn.CanReach(cell, PathEndMode.Touch, Danger.Unspecified))
            {
                return new FloatMenuOption("Cannot reach fishing spot", null);
            }

            var action = GetFishingAction(pawn, cell, fishingRod);
            return new FloatMenuOption($"Fish here ({targetTerrain.label})", action);
        }
        private static Action GetFishingAction(Pawn pawn, IntVec3 targetTerrain, Thing fishingRod)
        {
            void action()
            {
                var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("PW_Fish"), targetTerrain);
                job.targetB = fishingRod;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.MiscWork);
            }

            return action;
        }
    }
}
