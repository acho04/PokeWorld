using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using RimWorld;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace PokeWorld;

public class LevelTracker : IExposable
{
    public bool canEvolve;
    public CompPokemon comp;
    public int evolutionCountDown = 300;
    public List<PawnKindDef> evolutionDefList = [];
    public List<Evolution> evolutions;
    public string expCategory; // todo: add a setter with checks for max level?
    public int experience;
    public bool flagEverstoneAlertEvolution = true;
    public bool flagEverstoneOn;
    public bool flagIsEvolving;
    public int level; // todo: add a setter with checks for max level
    public Pawn pokemonHolder;
    public int totalExpForNextLevel;
    public int wildLevelMax;
    public int wildLevelMin;

    private static readonly Type RaceMorpher;
    private static readonly MethodInfo SwapAnimalToSapientVersion;
    private static IEnumerable<Faction> FactionsWithRoyalTitles => Find.FactionManager.AllFactions.Where((Faction f) => f.def.RoyalTitlesAwardableInSeniorityOrderForReading.Count > 0);

    static LevelTracker()
    {
        RaceMorpher = AccessTools.TypeByName("BigAndSmall.RaceMorpher");
        SwapAnimalToSapientVersion = RaceMorpher == null ? null : AccessTools.Method(RaceMorpher, "SwapAnimalToSapientVersion");
    }

    public LevelTracker(CompPokemon comp)
    {
        this.comp = comp;
        pokemonHolder = comp.Pokemon;
        canEvolve = comp.CanEvolve;
        evolutions = comp.Evolutions;
        expCategory = comp.ExpCategory;
        wildLevelMin = comp.WildLevelMin;
        wildLevelMax = comp.WildLevelMax;

        level = Rand.Range(wildLevelMin, wildLevelMax + 1);
        experience = 0;
        UpdateExpToNextLvl();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref experience, "PW_experience");
        Scribe_Values.Look(ref totalExpForNextLevel, "PW_totalExpForNextLevel", 1);
        Scribe_Values.Look(ref level, "PW_level", 1);
        Scribe_Values.Look(ref flagEverstoneOn, "PW_flagEverstoneOn");
        Scribe_Values.Look(ref flagEverstoneAlertEvolution, "PW_flagEverstoneAlertEvolution");
        Scribe_Values.Look(ref flagIsEvolving, "PW_flagIsEvolving");
        Scribe_Values.Look(ref evolutionCountDown, "PW_evolutionCountDown", 300);
        Scribe_Collections.Look(ref evolutionDefList, "PW_evolutionDefList", LookMode.Def);
    }

    public IEnumerable<Gizmo> GetGizmos()
    {
        if (pokemonHolder.Faction != Faction.OfPlayer) yield break;
        if (!comp.CanEvolve) yield break;

        if (flagIsEvolving)
        {
            yield return new Command_Action
            {
                action = delegate { CancelEvolution(true); },
                defaultLabel = "PW_StopEvolution".Translate(),
                defaultDesc = "PW_StopEvolutionDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc5,
                icon = TexCommand.ClearPrioritizedWork
            };
            yield break;
        }

        foreach (var evo in evolutions)
        {
            // todo: review this and maybe implement helpers
            if (evo.requirement != EvolutionRequirement.level) continue;
            if (evo.gender != Gender.None && pokemonHolder.gender != evo.gender) continue;
            var gen = evo.pawnKind.race.GetCompProperties<CompProperties_Pokemon>().generation;
            if (!PokeWorldSettings.GenerationAllowed(gen)) continue;

            yield return new Command_Toggle
            {
                defaultLabel = flagEverstoneOn
                    ? "PW_TakeBackEverstone".Translate()
                    : "PW_GiveEverstone".Translate(),
                defaultDesc = flagEverstoneOn
                    ? "PW_TakeBackEverstoneDesc".Translate()
                    : "PW_GiveEverstoneDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc5,
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/Everstone/Everstone"),
                toggleAction = delegate { flagEverstoneOn = !flagEverstoneOn; },
                isActive = () => flagEverstoneOn
            };

            break;
        }
    }

    public void IncreaseExperience(int expAmount)
    {
        // todo: move this into a setter on level
        if (level >= 100)
        {
            level = 100;
            experience = 0;
            return;
        }

        experience += expAmount;
        TryGainLevel();
    }

    public void TryGainLevel()
    {
        if (experience < totalExpForNextLevel) return;

        while (experience >= totalExpForNextLevel)
        {
            level++;
            if (pokemonHolder.Faction == Faction.OfPlayer)
            {
                comp.friendshipTracker.IncreaseFriendshipLevelUp();
                MoteMaker.ThrowText(pokemonHolder.DrawPos, pokemonHolder.Map, "PW_LevelIncrease".Translate(level));
                if (level % 10 == 0)
                    Messages.Message(
                        "PW_MessageLevelIncrease".Translate(pokemonHolder.Label, level), pokemonHolder,
                        MessageTypeDefOf.NeutralEvent
                    );
            }

            // todo: maybe move this above the level up message
            // todo: move this into a setter on level
            if (level >= 100)
            {
                level = 100;
                experience = 0;
                UpdateExpToNextLvl();
                break;
            }

            experience -= totalExpForNextLevel;
            UpdateExpToNextLvl();
        }

        comp.statTracker.UpdateStats();
        if (canEvolve && pokemonHolder.Faction == Faction.OfPlayer) TryEvolveAfterLevelUp();
    }

    public void UpdateExpToNextLvl()
    {
        totalExpForNextLevel = ExpTable.GetExpToNextLvl(expCategory, level);
    }

    public void TryEvolveAfterLevelUp()
    {
        // todo: refactor duplicated code with helper
        if (flagIsEvolving ||
            pokemonHolder.Map.designationManager.DesignationOn(pokemonHolder, DesignationDefOf.Slaughter) !=
            null) return;
        foreach (var evo in evolutions)
            if (PokeWorldSettings.GenerationAllowed(
                    evo.pawnKind.race.GetCompProperties<CompProperties_Pokemon>().generation
                )
                && evo.requirement == EvolutionRequirement.level && level >= evo.level
                && comp.friendshipTracker.EvolutionAllowed(evo.friendship)
                && (evo.gender == Gender.None || pokemonHolder.gender == evo.gender))
            {
                if (evo.otherRequirement != OtherEvolutionRequirement.none)
                {
                    var attack = (int)pokemonHolder.GetStatValue(DefDatabase<StatDef>.GetNamed("PW_Attack"));
                    var defense = (int)pokemonHolder.GetStatValue(DefDatabase<StatDef>.GetNamed("PW_Defense"));
                    if (evo.otherRequirement == OtherEvolutionRequirement.attack && attack <= defense)
                        continue;
                    if (evo.otherRequirement == OtherEvolutionRequirement.defense && defense <= attack)
                        continue;
                    if (evo.otherRequirement == OtherEvolutionRequirement.balanced && attack != defense)
                        continue;
                }

                var currentMapTime = GenLocalDate.HourOfDay(pokemonHolder.Map);
                if (evo.timeOfDay != TimeOfDay.Any
                    && (evo.timeOfDay != TimeOfDay.Day || currentMapTime is < 7 or >= 19)
                    && (evo.timeOfDay != TimeOfDay.Night || currentMapTime is < 19 and >= 7)) continue;
                
                if (!flagEverstoneOn)
                {
                    evolutionDefList.Add(evo.pawnKind);
                    BeginEvolutionProcess();
                }
                else if (flagEverstoneAlertEvolution)
                {
                    Messages.Message(
                        "PW_MessageEverstonePreventsEvolution".Translate(pokemonHolder.Label), 
                        pokemonHolder,
                        MessageTypeDefOf.NeutralEvent
                    );
                    flagEverstoneAlertEvolution = false;
                }
            }
    }

    public void TryEvolveWithItem(Thing item)
    {
        // todo: refactor duplicated code with helper
        if (flagIsEvolving ||
            pokemonHolder.Map.designationManager.DesignationOn(pokemonHolder, DesignationDefOf.Slaughter) !=
            null) return;

        foreach (var evo in evolutions.Where(evo => evo.item == item.def))
        {
            evolutionDefList.Add(evo.pawnKind);
            BeginEvolutionProcess();
        }
    }

    public void BeginEvolutionProcess()
    {
        // todo: refactor duplicated code with helper
        if (flagIsEvolving ||
            pokemonHolder.Map.designationManager.DesignationOn(pokemonHolder, DesignationDefOf.Slaughter) !=
            null) return;
        Messages.Message(
            "PW_MessageEvolving".Translate(pokemonHolder.Label), pokemonHolder, MessageTypeDefOf.NeutralEvent
        );
        flagIsEvolving = true;
    }

    public void UpdateEvolutionProcess()
    {
        // todo: refactor duplicated code with helper
        if (pokemonHolder.Map.designationManager.DesignationOn(pokemonHolder, DesignationDefOf.Slaughter) != null)
        {
            CancelEvolution(true);
            return;
        }

        if (evolutionCountDown % 60 == 0)
            for (var i = 0; i < 3; i++)
                FleckMaker.ThrowDustPuff(pokemonHolder.Position, pokemonHolder.Map, 2f);
        
        if (evolutionCountDown <= 0)
        {
            flagIsEvolving = false;
            Evolve(evolutionDefList);
        }

        evolutionCountDown -= 1;
    }

    public void CancelEvolution(bool scared = false)
    {
        Messages.Message(
            scared
                ? "PW_MessageScaredStoppedEvolving".Translate(pokemonHolder.Label)
                : "PW_MessageStoppedEvolving".Translate(pokemonHolder.Label), pokemonHolder,
            MessageTypeDefOf.NeutralEvent
        );
        flagIsEvolving = false;
        evolutionDefList.Clear();
        evolutionCountDown = 300;
    }

    public void Evolve(List<PawnKindDef> kindDefs)
    {
        var preEvoPokemon = pokemonHolder;
        var faction = pokemonHolder.Faction;
        foreach (var evolutionKindDef in kindDefs)
        {
            var postEvoPokemon = PawnGenerator.GeneratePawn(evolutionKindDef, faction);
            Copy(preEvoPokemon, postEvoPokemon);
            postEvoPokemon.health.Reset();
            GenSpawn.Spawn(postEvoPokemon, preEvoPokemon.Position, preEvoPokemon.Map);
            if (faction == Faction.OfPlayer)
                Find.World.GetComponent<PokedexManager>().AddPokemonKindCaught(postEvoPokemon.GetComp<CompPokemon>().PokedexNumber, postEvoPokemon.kindDef);
            if (SwapAnimalToSapientVersion != null && !preEvoPokemon.IsAnimal && postEvoPokemon.IsAnimal) //BigAndSmall detected, make our evolved form sapient.
            {
                Pawn result = SwapAnimalToSapientVersion.Invoke(null, [postEvoPokemon]) as Pawn;
                if (result != null)
                {
                    postEvoPokemon = result;
                }
            }
            if (!preEvoPokemon.IsAnimal && !postEvoPokemon.IsAnimal)
            {
                Copy_Sapient_Attributes(preEvoPokemon, postEvoPokemon);
            }
        }

        preEvoPokemon.inventory?.DropAllNearPawn(preEvoPokemon.Position);
        if (preEvoPokemon.carryTracker?.CarriedThing != null)
            preEvoPokemon.carryTracker?.TryDropCarriedThing(preEvoPokemon.Position, ThingPlaceMode.Near, out _);
        foreach (var hediff in preEvoPokemon.health.hediffSet?.hediffs ?? [])
        {
            if (hediff.def is not { countsAsAddedPartOrImplant: true }) continue;
            var part = hediff.Part;
            MedicalRecipesUtility.SpawnThingsFromHediffs(
                preEvoPokemon, part, preEvoPokemon.Position, preEvoPokemon.Map
            );
        }

        for (var i = 0; i < 10; i++) FleckMaker.ThrowDustPuff(preEvoPokemon.Position, preEvoPokemon.Map, 2f);
        preEvoPokemon.relations.ClearAllRelations();
        preEvoPokemon.Destroy();
    }

    private void Copy(Pawn pokemon, Pawn evolution)
    {
        // todo: refactor this for simplicity
        var postEvoComp = evolution.GetComp<CompPokemon>();
        postEvoComp.levelTracker.level = level;
        postEvoComp.levelTracker.experience = experience;
        postEvoComp.levelTracker.flagEverstoneOn = flagEverstoneOn;
        postEvoComp.levelTracker.UpdateExpToNextLvl();
        postEvoComp.friendshipTracker.friendship = comp.friendshipTracker.friendship;
        postEvoComp.friendshipTracker.flagMaxFriendshipMessage = comp.friendshipTracker.flagMaxFriendshipMessage;
        postEvoComp.ballDef = comp.ballDef;
        postEvoComp.statTracker.CopyPreEvoStat(comp);
        postEvoComp.statTracker.UpdateStats();
        postEvoComp.moveTracker.GetUnlockedMovesFromPreEvolution(comp);
        if (comp.shinyTracker.isShiny)
            postEvoComp.shinyTracker.MakeShiny();
        else
            postEvoComp.shinyTracker.isShiny = false;
        if (comp.formTracker != null && postEvoComp.formTracker != null)
            postEvoComp.formTracker.TryInheritFormFromPreEvo(comp.formTracker);
        if (evolution.RaceProps.hasGenders) evolution.gender = pokemon.gender;
        evolution.records = pokemon.records;
        evolution.relations.ClearAllRelations();
        foreach (var relation in pokemon.relations.DirectRelations.ToList())
            evolution.relations.AddDirectRelation(relation.def, relation.otherPawn);
        foreach (var relatedPawn in pokemon.relations.RelatedPawns.ToList())
        foreach (var otherDirectRelation in relatedPawn.relations.DirectRelations.ToList())
            if (otherDirectRelation.otherPawn == pokemon && !otherDirectRelation.def.reflexive)
                relatedPawn.relations.AddDirectRelation(otherDirectRelation.def, evolution);
        evolution.ageTracker.AgeBiologicalTicks = pokemon.ageTracker.AgeBiologicalTicks;
        evolution.ageTracker.AgeChronologicalTicks = pokemon.ageTracker.AgeChronologicalTicks;
        evolution.ageTracker.BirthAbsTicks = pokemon.ageTracker.BirthAbsTicks;
        foreach (var td in DefDatabase<TrainableDef>.AllDefs)
            if (evolution.training.CanBeTrained(td) && pokemon?.training != null)
            {
                if (pokemon.training.HasLearned(td)) evolution.training.Train(td, null, true);
                if (pokemon.training.GetWanted(td)) evolution.training.SetWantedRecursive(td, true);
            }

        evolution.playerSettings.animalsReleased = pokemon.playerSettings.animalsReleased;
        evolution.playerSettings.AreaRestrictionInPawnCurrentMap =
            pokemon.playerSettings.AreaRestrictionInPawnCurrentMap;
        evolution.playerSettings.displayOrder = pokemon.playerSettings.displayOrder;
        evolution.playerSettings.followDrafted = pokemon.playerSettings.followDrafted;
        evolution.playerSettings.followFieldwork = pokemon.playerSettings.followFieldwork;
        evolution.playerSettings.hostilityResponse = pokemon.playerSettings.hostilityResponse;
        evolution.playerSettings.joinTick = pokemon.playerSettings.joinTick;
        evolution.playerSettings.Master = pokemon.playerSettings.Master;
        evolution.playerSettings.medCare = pokemon.playerSettings.medCare;
        if (pokemon.Name is { Numerical: false }) evolution.Name = pokemon.Name;
    }

    //We only need to copy this stuff if both the existing mon and evolution are humanlike
    private void Copy_Sapient_Attributes(Pawn pokemon, Pawn evolution)
    {
        evolution.gender = pokemon.gender;

        //Copy Age
        evolution.ageTracker.AgeChronologicalTicks = pokemon.ageTracker.AgeChronologicalTicks;
        if (pokemon.ageTracker.AgeBiologicalYears < 3)
        {
            evolution.ageTracker.AgeBiologicalTicks = 3 * GenDate.TicksPerYear;
        }
        else
        {
            evolution.ageTracker.AgeBiologicalTicks = pokemon.ageTracker.AgeBiologicalTicks;
        }

        //Copy Ideology
        if (ModsConfig.IdeologyActive && pokemon.ideo != null && evolution.ideo != null)
        {
            evolution.ideo.SetIdeo(pokemon.Ideo);
            evolution.ideo.OffsetCertainty(pokemon.ideo.Certainty - evolution.ideo.Certainty);
        }

        //Copy backstory and traits
        if (evolution.story != null && pokemon.story != null)
        {
            evolution.story.favoriteColor = pokemon.story.favoriteColor;
            evolution.story.Childhood = pokemon.story.Childhood;
            evolution.story.Adulthood = pokemon.story.Adulthood;
            evolution.story.traits.allTraits.Clear();
            foreach (Trait allTrait in pokemon.story.traits.allTraits)
            {
                if (!ModsConfig.BiotechActive || allTrait.sourceGene == null)
                {
                    evolution.story.traits.GainTrait(new Trait(allTrait.def, allTrait.Degree, allTrait.ScenForced));
                }
            }
        }

        //Copy genes
        if (ModsConfig.BiotechActive && evolution.genes != null && pokemon.genes != null)
        {
            evolution.genes.Xenogenes.Clear();
            List<Gene> sourceXenogenes = pokemon.genes.Xenogenes;
            foreach (Gene sourceXeno in sourceXenogenes)
            {
                evolution.genes.AddGene(sourceXeno.def, xenogene: true);
            }
            int i;
            for (i = 0; i < sourceXenogenes.Count; i++)
            {
                Gene xenogene = evolution.genes.Xenogenes[i];
                if (sourceXenogenes[i].Overridden)
                {
                    xenogene.overriddenByGene = evolution.genes.GenesListForReading.First((Gene e) => e.def == sourceXenogenes[i].overriddenByGene.def);
                }
                else
                {
                    xenogene.overriddenByGene = null;
                }
            }
            evolution.genes.Endogenes.Clear();
            List<Gene> sourceEndogenes = pokemon.genes.Endogenes;
            foreach (Gene sourceEndo in sourceEndogenes)
            {
                evolution.genes.AddGene(sourceEndo.def, xenogene: false);
            }
            int i2;
            for (i2 = 0; i2 < sourceEndogenes.Count; i2++)
            {
                Gene endogene = evolution.genes.Endogenes[i2];
                if (sourceEndogenes[i2].Overridden)
                {
                    endogene.overriddenByGene = evolution.genes.GenesListForReading.First((Gene e) => e.def == sourceEndogenes[i2].overriddenByGene.def);
                }
                else
                {
                    endogene.overriddenByGene = null;
                }
            }
        }

        //Copy skills
        evolution.skills.skills.Clear();
        foreach (SkillRecord skill in pokemon.skills.skills)
        {
            SkillRecord item = new SkillRecord(evolution, skill.def)
            {
                levelInt = skill.levelInt,
                passion = skill.passion,
                xpSinceLastLevel = skill.xpSinceLastLevel,
                xpSinceMidnight = skill.xpSinceMidnight
            };
            evolution.skills.skills.Add(item);
        }

        //Copy needs
        evolution.needs.AllNeeds.Clear();
        foreach (Need allNeed in pokemon.needs.AllNeeds)
        {
            Need need = (Need)Activator.CreateInstance(allNeed.def.needClass, evolution);
            need.def = allNeed.def;
            evolution.needs.AllNeeds.Add(need);
            need.SetInitialLevel();
            need.CurLevel = allNeed.CurLevel;
            evolution.needs.BindDirectNeedFields();
        }
        if (pokemon.needs.mood != null)
        {
            List<Thought_Memory> memories = evolution.needs.mood.thoughts.memories.Memories;
            memories.Clear();
            foreach (Thought_Memory memory in pokemon.needs.mood.thoughts.memories.Memories)
            {
                Thought_Memory thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(memory.def);
                thought_Memory.CopyFrom(memory);
                thought_Memory.pawn = evolution;
                memories.Add(thought_Memory);
            }
        }


        //Copy mutant
        if (pokemon.mutant != null)
            MutantUtility.SetPawnAsMutantInstantly(evolution, pokemon.mutant.Def, pokemon.GetRotStage());

        //Copy royalty
        if (pokemon.royalty != null && evolution.royalty != null)
        {
            foreach (RoyalTitle item in pokemon.royalty.AllTitlesForReading)
            {
                foreach (Faction royalFaction in FactionsWithRoyalTitles)
                {
                    evolution.royalty.SetHeir(pokemon.royalty.GetHeir(royalFaction), royalFaction);
                    foreach(FactionPermit permit in pokemon.royalty.PermitsFromFaction(royalFaction))
                    {
                        evolution.royalty.AddPermit(permit.Permit, royalFaction);
                    }
                    RoyalTitle title = pokemon.royalty.GetCurrentTitleInFaction(royalFaction);
                    if (title != null)
                        evolution.royalty.SetTitle(royalFaction, title.def, true, true, false);
                    evolution.royalty.SetFavor(royalFaction, pokemon.royalty.GetFavor(royalFaction));
                }
            }
        }

        //Copy Abilities
        foreach (Ability ability in pokemon.abilities.abilities)
        {
            if (evolution.abilities.GetAbility(ability.def) == null)
                evolution.abilities.GainAbility(ability.def);
        }
        List<Ability> abilities = evolution.abilities.abilities;
        for (int i = abilities.Count - 1; i >= 0; i--)
        {
            Ability ability2 = abilities[i];
            if (pokemon.abilities.GetAbility(ability2.def) == null)
                evolution.abilities.RemoveAbility(ability2.def);
        }

    }

    public void LevelTick()
    {
        if (flagIsEvolving) UpdateEvolutionProcess();
    }
}
