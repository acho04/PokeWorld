using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PokeWorld;

public class MoveTracker : IExposable
{
    public CompPokemon comp;
    private List<Verb> initializedVerbs;
    public MoveDef lastUsedMove;
    public Pawn pokemonHolder;
    //All potentially learnable moves
    public Dictionary<MoveDef, MoveLearnMethod> learnableMoves;
    //Moves unlockable via level up
    public Dictionary<MoveDef, int> unlockableMoves;
    //Moves taught via external means
    public List<MoveDef> tutoredMoves;
    //Whether a pokemon wants to use a move or not
    public Dictionary<MoveDef, bool> wantedMoves;
    //Moves visible on the moves tab
    public List<MoveDef> visibleMoves => unlockableMoves.Keys.Concat(tutoredMoves).Distinct().ToList();
    //Moves that the pokemon knows
    public List<MoveDef> knownMoves => unlockableMoves.Keys.Where(move => unlockableMoves[move] <= comp.levelTracker.level).Concat(tutoredMoves).Distinct().ToList();

    public MoveTracker(CompPokemon comp)
    {
        this.comp = comp;
        pokemonHolder = (Pawn)comp.parent;
        learnableMoves = new Dictionary<MoveDef, MoveLearnMethod>();
        unlockableMoves = new Dictionary<MoveDef, int>();
        wantedMoves = new Dictionary<MoveDef, bool>();
        tutoredMoves = new List<MoveDef>();
        initializedVerbs = new List<Verb>();
        foreach (var move in comp.Moves)
        {
            learnableMoves.Add(move.moveDef, move.learnMethod);
            if (move.learnMethod.HasFlag(MoveLearnMethod.Level))
                unlockableMoves.Add(move.moveDef, move.unlockLevel);
            if (move.learnMethod.HasFlag(MoveLearnMethod.AlwaysKnown))
                tutoredMoves.Add(move.moveDef);
            wantedMoves.Add(move.moveDef, true);
        }
        if (!learnableMoves.Keys.Contains(DefDatabase<MoveDef>.GetNamed("Struggle")))
        {
            learnableMoves.Add(DefDatabase<MoveDef>.GetNamed("Struggle"), MoveLearnMethod.AlwaysKnown);
            tutoredMoves.Add(DefDatabase<MoveDef>.GetNamed("Struggle"));
            wantedMoves.Add(DefDatabase<MoveDef>.GetNamed("Struggle"), true);
        }

        OrderMoves();
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref unlockableMoves, "unlockableMoves", LookMode.Def, LookMode.Value);
        Scribe_Collections.Look(ref learnableMoves, "learnableMoves", LookMode.Def, LookMode.Value);
        Scribe_Collections.Look(ref tutoredMoves, "tutoredMoves", LookMode.Def);
        Scribe_Collections.Look(ref wantedMoves, "wantedMoves", LookMode.Def, LookMode.Value);
        Scribe_Collections.Look(ref initializedVerbs, "initializedVerbs", LookMode.Deep);
        Scribe_Defs.Look(ref lastUsedMove, "lastUsedMove");

        // Update missing data
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var flag = false;
            if (learnableMoves == null)
            {
                learnableMoves = new Dictionary<MoveDef, MoveLearnMethod>();
                flag = true;
            }
            if (tutoredMoves == null)
            {
                tutoredMoves = new List<MoveDef>();
                flag = true;
            }
            if (flag)
            {
                unlockableMoves.Clear();
                wantedMoves.Clear();
                foreach (var move in comp.Moves)
                {
                    learnableMoves.Add(move.moveDef, move.learnMethod);
                    if (move.learnMethod.HasFlag(MoveLearnMethod.Level))
                        unlockableMoves.Add(move.moveDef, move.unlockLevel);
                    if (move.learnMethod.HasFlag(MoveLearnMethod.AlwaysKnown))
                        tutoredMoves.Add(move.moveDef);
                    wantedMoves.Add(move.moveDef, true);
                }
            }
        }
    }

    public IEnumerable<Gizmo> GetGizmos()
    {
        if (PokemonMasterUtility.IsPokemonDrafted(pokemonHolder))
            foreach (var attackGizmo in PokemonAttackGizmoUtility.GetAttackGizmos(pokemonHolder))
                yield return attackGizmo;
    }

    public bool CanBeTaughtMove(MoveDef moveDef, MoveLearnMethod byMethod)
    {
        if (!learnableMoves.TryGetValue(moveDef, out var method)) return false;
        if (HasUnlocked(moveDef)) return false;
        return (method & byMethod) != 0;
    }

    public void TeachMove(MoveDef moveDef, MoveLearnMethod byMethod)
    {
        if (!CanBeTaughtMove(moveDef, byMethod)) return;
        if (unlockableMoves.ContainsKey(moveDef))
            unlockableMoves.Remove(moveDef);
        tutoredMoves.Add(moveDef);
    }

    public bool HasUnlocked(MoveDef moveDef)
    {
        if (unlockableMoves.TryGetValue(moveDef, out var unlockLevel))
            if (unlockLevel <= comp.levelTracker.level)
                return true;
        if (tutoredMoves.Contains(moveDef)) return true;
        return false;
    }

    public bool GetWanted(MoveDef moveDef)
    {
        if (wantedMoves.ContainsKey(moveDef)) return wantedMoves[moveDef];
        return false;
    }

    public void SetWanted(MoveDef moveDef, bool wanted)
    {
        if (wantedMoves.ContainsKey(moveDef)) wantedMoves[moveDef] = wanted;
    }

    public void GetUnlockedMovesFromPreEvolution(CompPokemon preEvoComp)
    {
        foreach (var kvp in preEvoComp.moveTracker.unlockableMoves)
        {
            if (kvp.Key == DefDatabase<MoveDef>.GetNamed("Struggle")) continue;
            if (preEvoComp.moveTracker.HasUnlocked(kvp.Key))
            {
                if (unlockableMoves.Keys.Contains(kvp.Key))
                {
                    unlockableMoves.Remove(kvp.Key);
                }

                unlockableMoves.Add(kvp.Key, kvp.Value);
                wantedMoves[kvp.Key] = preEvoComp.moveTracker.wantedMoves[kvp.Key];
            }
        }
        foreach (var move in preEvoComp.moveTracker.tutoredMoves)
        {
            if (unlockableMoves.Keys.Contains(move))
            {
                unlockableMoves.Remove(move);
            }
            if (!tutoredMoves.Contains(move))
            {
                tutoredMoves.Add(move);
            }
        }

        OrderMoves();
    }

    private void OrderMoves()
    {
        var myList = unlockableMoves.ToList();
        myList.Sort(
            delegate(
                KeyValuePair<MoveDef, int> pair1,
                KeyValuePair<MoveDef, int> pair2
            )
            {
                return pair1.Value.CompareTo(pair2.Value);
            }
        );
        unlockableMoves = myList.ToDictionary(x => x.Key, x => x.Value);
    }
}
