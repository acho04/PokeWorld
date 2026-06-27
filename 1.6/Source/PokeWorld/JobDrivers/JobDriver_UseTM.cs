using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace PokeWorld
{
    internal class JobDriver_UseTM : JobDriver_UseItem
    {

        private Technical_Machine TM => (Technical_Machine)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TM, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            //Log.Error("Error1");
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOn(() => !base.TargetThingA.TryGetComp<CompUsable>().CanBeUsedBy(pawn));            
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_General.Do(Teach);
        }

        private void Teach()
        {
            //Log.Error("Error1");
            if (TM?.move != null)
            {
                //Log.Error("Error2");
                foreach (Map existingMap in Current.Game.Maps.ToList())
                {
                    //Log.Error("Error3");
                    foreach(Pawn pawn in existingMap.mapPawns.ColonyAnimals.ToList())
                    {
                        //Log.Error("Error4");  
                        var x = pawn.TryGetComp<CompPokemon>();
                        if (x != null)
                        {
                           // Log.Error("Error5");
                            x.moveTracker.TeachMove(TM.move,MoveLearnMethod.Tutor);                    
                        }
                    }
                }
                Find.LetterStack.ReceiveLetter(
                    "PW_AppliedTM".Translate(), "PW_AppliedTMDesc".Translate(TM.move),
                    LetterDefOf.PositiveEvent
                );    
                TM.Destroy();                
            }
        }
    }
}
