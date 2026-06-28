using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static HarmonyLib.Code;

namespace PokeWorld
{
    
    public class Technical_Machine : ThingWithComps
    {
        public TMDef Def => (TMDef)this.def;
        public MoveDef move;
        private Graphic new_graphic;
        public override string LabelNoCount => move != null ? base.LabelNoCount + ": " + move.label : base.LabelNoCount;
        public override string DescriptionDetailed => move != null ? base.DescriptionDetailed + " This one teaches the move " + move.label + "." : base.DescriptionDetailed;
        public override Graphic Graphic => move != null ? new_graphic ?? GenerateGraphicFromMove() : base.Graphic;

        public override void PostMake()
        {
            base.PostMake();
            if (unlockedMoveTracker.unlockedMoves.Count() == Def.TM_Moves.Count())
            {
                SetMove(Def.TM_Moves.RandomElement());
            }
            else
            {
                while (move == null)
                {
                    MoveDef x = Def.TM_Moves.RandomElement();
                    if (!unlockedMoveTracker.unlockedMoves.Contains(x)) SetMove(x);
                } 
            }
            
        }


        public void SetMove(MoveDef md)
        {
            move = md;
            new_graphic = null;
        }

        public override bool CanStackWith(Thing other)
        {
            return false;
        }

        private Graphic GenerateGraphicFromMove()
        {
            var graphicData = new GraphicData();
            graphicData.CopyFrom(base.Graphic.data);
            graphicData.texPath = move?.type?.tmTexturePath;
            new_graphic = graphicData.Graphic;
            return new_graphic;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref move, "move");
        }
    }
}
