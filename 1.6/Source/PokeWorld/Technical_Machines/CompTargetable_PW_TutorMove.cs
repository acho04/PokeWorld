using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PokeWorld
{
    public class CompTargetable_PW_TutorMove : CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;

        public new CompProperties_Targetable_PW_TutorMove Props => (CompProperties_Targetable_PW_TutorMove)props;

        protected override TargetingParameters GetTargetingParameters()
        {
            Technical_Machine TM = (Technical_Machine)parent;
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetAnimals = true,
                canTargetHumans = true,
                canTargetMechs = false,
                validator = (TargetInfo target) => (target.Thing is Pawn p &&
                                                    p.TryGetComp<CompPokemon>() is CompPokemon comp &&
                                                    comp.moveTracker.CanBeTaughtMove(TM.move, MoveLearnMethod.Tutor))
            };
        }

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            yield return targetChosenByPlayer;
        }
    }

    public class CompProperties_Targetable_PW_TutorMove : CompProperties_Targetable
    {
        public CompProperties_Targetable_PW_TutorMove()
        {
            compClass = typeof(CompTargetable_PW_TutorMove);
        }
    }
}
