using RimWorld;
using Verse;
using RimWorld.Planet;
using System.Collections.Generic;
namespace PokeWorld;

public class unlockedMoveTracker : WorldComponent
{
    public static List<MoveDef> unlockedMoves;
    public unlockedMoveTracker (World world) : base(world)
    {
        if (unlockedMoves == null)
        {
            unlockedMoves = new List<MoveDef>();
        }
    }
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref unlockedMoves, "unlockedMoves", LookMode.Def);
    }
}


