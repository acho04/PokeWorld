using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace PokeWorld
{
    internal class PokedexEntry : IExposable
    {
        public int dexNumber;
        public Dictionary<PawnKindDef, PokemonPokedexState> variants = new Dictionary<PawnKindDef, PokemonPokedexState>();
        public bool seen => variants.ContainsValue(PokemonPokedexState.Seen) || variants.ContainsValue(PokemonPokedexState.Caught);
        public bool caught => variants.ContainsValue(PokemonPokedexState.Caught);
        
        public PokedexEntry()
        {
            this.variants = new();
        }

        public PokedexEntry(int dexNumber)
        {
            this.dexNumber = dexNumber;
            this.variants = new();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref dexNumber, "PW_pokedexEntryNumber");
            Scribe_Collections.Look(ref variants, "PW_pokedexEntryVariants", LookMode.Def, LookMode.Value);
        }
    }
}
