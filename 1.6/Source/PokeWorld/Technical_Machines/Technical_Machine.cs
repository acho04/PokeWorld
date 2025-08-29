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
        public override Graphic Graphic => move != null ? new_graphic ?? GenerateGraphicFromMove() : base.Graphic;

        public override void PostMake()
        {
            base.PostMake();
            if (move == null)
                SetMove(Def.TM_Moves.RandomElement());
        }


        public void SetMove(MoveDef md)
        {
            move = md;
            new_graphic = null;
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
