using RimWorld.Planet;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Verse;

namespace PokeWorld;

public sealed class PokedexManager(World world) : WorldComponent(world)
{
    private Dictionary<int, PawnKindDef> discoveredForm = new();
    private Dictionary<PawnKindDef, PokemonPokedexState> pokedex = new();

    public int TotalSeen()
    {
        return pokedex.Values.Count(x => x is PokemonPokedexState.Seen or PokemonPokedexState.Caught);
    }

    public int TotalSeen(int generation, bool includeLegendaries = true)
    {
        if (includeLegendaries)
            return pokedex.Count(
                x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                     x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                     x.Value is PokemonPokedexState.Seen or PokemonPokedexState.Caught
            );

        return pokedex.Count(
            x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                 x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                 !x.Key.race.GetCompProperties<CompProperties_Pokemon>().attributes
                     .Contains(PokemonAttribute.Legendary) &&
                 x.Value is PokemonPokedexState.Seen or PokemonPokedexState.Caught
        );
    }

    public int TotalCaught()
    {
        return pokedex.Values.Count(x => x == PokemonPokedexState.Caught);
    }

    public int TotalCaught(int generation, bool includeLegendaries = true)
    {
        if (includeLegendaries)
            return pokedex.Count(
                x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                     x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                     x.Value == PokemonPokedexState.Caught
            );
        return pokedex.Count(
            x => x.Key.race.HasComp(typeof(CompPokemon)) &&
                 x.Key.race.GetCompProperties<CompProperties_Pokemon>().generation == generation &&
                 !x.Key.race.GetCompProperties<CompProperties_Pokemon>().attributes
                     .Contains(PokemonAttribute.Legendary) && x.Value == PokemonPokedexState.Caught
        );
    }

    public override void ExposeData()
    { 
        Scribe_Collections.Look(ref discoveredForm, "PW_discoverdForms", LookMode.Value, LookMode.Def);
        Scribe_Collections.Look(ref pokedex, "PW_pokedex", LookMode.Def, LookMode.Value);
        
        //Fix any potential corruption
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (discoveredForm == null)
                discoveredForm = new();
            if (pokedex == null)
                pokedex = new();

            foreach (var kvp in pokedex)
            {
                var pawnKind = kvp.Key;
                if (!pawnKind.race.HasComp(typeof(CompPokemon)))
                    continue;
                var dexNumber = pawnKind.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber;
                if (!discoveredForm.ContainsKey(dexNumber))
                    discoveredForm.Add(dexNumber, kvp.Key);
            }
            foreach (var kvp in discoveredForm)
            {
                var pawnKind = kvp.Value;
                if (!pokedex.ContainsKey(pawnKind))
                    discoveredForm.Remove(kvp.Key);
            }
        }
    }

    public bool IsPokemonSeen(int dexNumber)
    {
        if (!discoveredForm.TryGetValue(dexNumber, out var pawnKind)) return false;
        pokedex.TryGetValue(pawnKind, out var value);
        return value == PokemonPokedexState.Seen || value == PokemonPokedexState.Caught;
    }

    public bool IsPokemonCaught(int dexNumber)
    {
        if (!discoveredForm.TryGetValue(dexNumber, out var pawnKind)) return false;
        pokedex.TryGetValue(pawnKind, out var value);
        return value == PokemonPokedexState.Caught;
    }

    public void AddPokemonKindSeen(int dexNumber, PawnKindDef pawnKind)
    {
        if (!pawnKind.race.HasComp(typeof(CompPokemon))) return;

        if (!discoveredForm.TryGetValue(dexNumber, out var baseKind))
        {
            discoveredForm[dexNumber] = pawnKind;
            pokedex[pawnKind] = PokemonPokedexState.Seen;
        }
        else if (!pokedex.ContainsKey(baseKind) || pokedex[baseKind] == PokemonPokedexState.None)
            pokedex[baseKind] = PokemonPokedexState.Seen;
    }

    public void AddPokemonKindCaught(int dexNumber, PawnKindDef pawnKind)
    {
        if (!pawnKind.race.HasComp(typeof(CompPokemon))) return;
        if (!discoveredForm.TryGetValue(dexNumber, out var baseKind))
        {
            discoveredForm[dexNumber] = pawnKind;
            pokedex[pawnKind] = PokemonPokedexState.Caught;
            return;
        }
        pokedex[baseKind] = PokemonPokedexState.Caught;
    }

    public void DebugFillPokedex()
    {
        foreach (var allDef in DefDatabase<PawnKindDef>.AllDefs.Where(x => x.race.HasComp(typeof(CompPokemon))))
            AddPokemonKindCaught(allDef.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber, allDef);
    }

    public void DebugFillPokedexNoLegendary()
    {
        foreach (var allDef in DefDatabase<PawnKindDef>.AllDefs.Where(
                     x => x.race.HasComp(typeof(CompPokemon)) && !x.race.GetCompProperties<CompProperties_Pokemon>()
                         .attributes.Contains(PokemonAttribute.Legendary)
                 )) AddPokemonKindCaught(allDef.race.GetCompProperties<CompProperties_Pokemon>().pokedexNumber, allDef);
    }
}
