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
    internal class JobDriver_UseItemOnPokemon : JobDriver_UseItem
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, job.count, null, errorOnFailed))
                return false;

            if (job.targetB.IsValid && !pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOn(() => !base.TargetThingA.TryGetComp<CompUsable>().CanBeUsedBy(pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, base.TargetThingA.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch);

            if (job.targetB.IsValid)
            {
                yield return Toils_Haul.StartCarryThing(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B);
            }

        }
    }
}
