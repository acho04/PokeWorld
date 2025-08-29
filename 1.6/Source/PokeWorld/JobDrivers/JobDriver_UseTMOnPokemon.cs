using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace PokeWorld
{
    internal class JobDriver_UseTMOnPokemon : JobDriver_UseItem
    {

        private Pawn TargetPawn => (Pawn)job.GetTarget(TargetIndex.B).Thing;
        private Technical_Machine TM => (Technical_Machine)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(TM, job, 1, -1, null, errorOnFailed);
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOn(() => !base.TargetThingA.TryGetComp<CompUsable>().CanBeUsedBy(pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A).FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_General.Do(Teach);
        }

        private void Teach()
        {

            if (TM?.move != null && TargetPawn.TryGetComp<CompPokemon>() is CompPokemon comp)
                comp.moveTracker.TeachMove(TM.move, MoveLearnMethod.Tutor);
        }
    }
}
