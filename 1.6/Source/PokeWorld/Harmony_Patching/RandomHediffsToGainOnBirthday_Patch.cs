using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PokeWorld
{
    [HarmonyPatch(typeof(AgeInjuryUtility), "RandomHediffsToGainOnBirthday", typeof(ThingDef), typeof(float), typeof(float))]
    internal class RandomHediffsToGainOnBirthday_Patch
    {
        //For scaling the probabilities for humans back to vanilla values
        const float humanLifeExpectancy = 80f;

        // Kill this stupid broken function that doesn't work for long lived pawns
        static bool Prefix(ThingDef raceDef, float cancerFactor, float age,
                       ref IEnumerable<HediffGiver_Birthday> __result)
        {
            __result = PatchedRandomHediffsToGainOnBirthday(raceDef, cancerFactor, age);
            return false;
        }

        private static IEnumerable<HediffGiver_Birthday> PatchedRandomHediffsToGainOnBirthday(
            ThingDef raceDef, float cancerFactor, float age)
        {
            List<HediffGiverSetDef> sets = raceDef.race.hediffGiverSets;
            if (sets == null)
                yield break;

            float lifeExpectancy = raceDef.race.lifeExpectancy;

            float fracNow = age / lifeExpectancy;

            for (int i = 0; i < sets.Count; i++)
            {
                List<HediffGiver> givers = sets[i].hediffGivers;
                if (givers == null)
                    continue;

                for (int j = 0; j < givers.Count; j++)
                {
                    if (givers[j] is not HediffGiver_Birthday giver)
                        continue;

                    float fNow = fracNow;

                    if (giver.hediff == HediffDefOf.Carcinoma)
                    {
                        fNow *= cancerFactor;
                    }

                    // Scale by lifeExpectancy to account for longer lived pawns having way more birthdays.
                    float probNow = giver.ageFractionChanceCurve.Evaluate(fNow);
                    float probability = probNow * humanLifeExpectancy / lifeExpectancy;

                    if (Rand.Value < probability)
                        yield return giver;
                }
            }
        }
    }
}
