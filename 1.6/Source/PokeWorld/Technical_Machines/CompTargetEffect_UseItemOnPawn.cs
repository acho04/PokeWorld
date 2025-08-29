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
    public class CompTargetEffect_UseItemOnPawn : CompTargetEffect
    {
        public CompProperties_TargetEffect_UseItemOnPawn Props => (CompProperties_TargetEffect_UseItemOnPawn)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (user.IsColonistPlayerControlled)
            {
                Job job = JobMaker.MakeJob(Props.useJob, parent, target);
                job.count = 1;
                job.playerForced = true;
                user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
    }

    public class CompProperties_TargetEffect_UseItemOnPawn : CompProperties
    {
        public JobDef useJob;
        public CompProperties_TargetEffect_UseItemOnPawn()
        {
            compClass = typeof(CompTargetEffect_UseItemOnPawn);
        }
    }
}
