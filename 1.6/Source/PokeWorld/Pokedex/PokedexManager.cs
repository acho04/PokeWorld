using RimWorld.Planet;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Verse;

namespace PokeWorld;

public sealed class PokedexManager(World world) : WorldComponent(world)
{
    private Dictionary<int, PokedexEntry> pokedex = new Dictionary<int, PokedexEntry>();
    
    
    private Dictionary<int, PawnKindDef> deprecated_discoveredForm = new();
    private Dictionary<PawnKindDef, PokemonPokedexState> deprecated_pokedex = new();

    public int TotalSeen()
    {
        return pokedex.Values.Count(x => x.seen);
    }

    public int TotalSeen(int generation, bool includeLegendaries = true)
    {
        if (includeLegendaries)
            return pokedex.Values.Count(
                v => v.variants.Any(x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                         x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                         x.Value is PokemonPokedexState.Seen or PokemonPokedexState.Caught)
            );

        return pokedex.Values.Count(
            v => v.variants.Any(x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                                x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                                !x.Key.race.GetCompProperties<CompProperties_Pokemon>().attributes
                                    .Contains(PokemonAttribute.Legendary) &&
                                x.Value is PokemonPokedexState.Seen or PokemonPokedexState.Caught)
        );
    }

    public int TotalCaught()
    {
        return pokedex.Values.Count(x => x.caught);
    }

    public int TotalCaught(int generation, bool includeLegendaries = true)
    {
        if (includeLegendaries)
            return pokedex.Values.Count(
                v => v.variants.Any(x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                                    x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                                    x.Value == PokemonPokedexState.Caught)
            );
        return pokedex.Values.Count(
            v => v.variants.Any(x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                                x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                                !x.Key.race.GetCompProperties<CompProperties_Pokemon>().attributes.Contains(PokemonAttribute.Legendary) &&
                                x.Value == PokemonPokedexState.Caught)
        );
    }

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Scribe_Collections.Look(ref deprecated_discoveredForm, "PW_discoverdForms", LookMode.Value, LookMode.Def);
            Scribe_Collections.Look(ref deprecated_pokedex, "PW_pokedex", LookMode.Def, LookMode.Value);
        }

        Scribe_Collections.Look(ref pokedex, "PW_pokedexEntries", LookMode.Value, LookMode.Deep);
        
        //Fix any potential corruption
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (pokedex == null)
                pokedex = new();

            if(deprecated_pokedex != null)
            {
                foreach (var kvp in deprecated_pokedex)
                {
                    var pawnKind = kvp.Key;
                    if (!pawnKind.race.HasComp(typeof(CompPokemon)))
                        continue;
                    var dexNumber = pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber;
                    if (!pokedex.ContainsKey(dexNumber))
                    {
                        PokedexEntry newEntry = new PokedexEntry(dexNumber);
                        newEntry.variants.Add(kvp.Key, kvp.Value);
                        pokedex.Add(dexNumber, newEntry);
                    }
                }
            }
        }
    }

    public bool IsPokemonSeen(int dexNumber)
    {
        if (!pokedex.TryGetValue(dexNumber, out var dexEntry)) return false;
        return dexEntry.seen;
    }

    public bool IsPokemonSeen(PawnKindDef pawnKind)
    {
        if(!pokedex.TryGetValue(pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber, out var dexEntry)) return false;
        dexEntry.variants.TryGetValue(pawnKind, out var state);
        return state == PokemonPokedexState.Seen || state == PokemonPokedexState.Caught;
    }

    public bool IsPokemonCaught(int dexNumber)
    {
        if (!pokedex.TryGetValue(dexNumber, out var dexEntry)) return false;
        return dexEntry.caught;
    }

    public bool IsPokemonCaught(PawnKindDef pawnKind)
    {
        if (!pokedex.TryGetValue(pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber, out var dexEntry)) return false;
        dexEntry.variants.TryGetValue(pawnKind, out var state);
        return state == PokemonPokedexState.Caught;
    }

    public void AddPokemonKindSeen(PawnKindDef pawnKind)
    {
        if (!pawnKind.race.HasComp(typeof(CompPokemon))) return;

        var dexNumber = pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber;
        if (!pokedex.TryGetValue(dexNumber, out var dexEntry))
        {
            dexEntry = new(dexNumber);
            dexEntry.variants.Add(pawnKind, PokemonPokedexState.Seen);
            pokedex[dexNumber] = dexEntry;
            return;
        }
        if (!dexEntry.variants.TryGetValue(pawnKind, out var value) || (value != PokemonPokedexState.Seen && value != PokemonPokedexState.Caught))
            dexEntry.variants[pawnKind] = PokemonPokedexState.Seen;
    }

    public void AddPokemonKindCaught(PawnKindDef pawnKind)
    {
        if (!pawnKind.race.HasComp(typeof(CompPokemon))) return;

        var dexNumber = pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber;
        if (!pokedex.TryGetValue(dexNumber, out var dexEntry))
        {
            dexEntry = new(dexNumber);
            dexEntry.variants.Add(pawnKind, PokemonPokedexState.Caught);
            pokedex[dexNumber] = dexEntry;
            return;
        }
        if (!dexEntry.variants.TryGetValue(pawnKind, out var value) || value != PokemonPokedexState.Caught)
            dexEntry.variants[pawnKind] = PokemonPokedexState.Caught;
    }

    public void DebugFillPokedex()
    {
        foreach (var allDef in DefDatabase<PawnKindDef>.AllDefs.Where(x => x.race.HasComp(typeof(CompPokemon))))
            AddPokemonKindCaught(allDef);
    }

    public void DebugFillPokedexNoLegendary()
    {
        foreach (var allDef in DefDatabase<PawnKindDef>.AllDefs.Where(
                     x => x.race.HasComp(typeof(CompPokemon)) && !x.race.GetCompProperties<CompProperties_Pokemon>()
                         .attributes.Contains(PokemonAttribute.Legendary)
                 )) AddPokemonKindCaught(allDef);
    }
}
