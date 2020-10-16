using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SekaiCalculator
{
    public class Note
    {
        public float frame;
        public float Weight
        {
            get
            {
                switch (type)
                {
                    case NoteType.Skill: return 0;
                    case NoteType.FeverBegin: return 0;
                    case NoteType.FeverStart: return 0;

                    case NoteType.Normal: return 10;
                    case NoteType.Long: return 10;
                    case NoteType.Mid: return 1;
                    case NoteType.Flick: return 10;

                    case NoteType.Critical: return 20;
                    case NoteType.Critical | NoteType.Long: return 20;
                    case NoteType.Critical | NoteType.Mid: return 2;
                    case NoteType.Critical | NoteType.Flick: return 30;

                    case NoteType.Auto: return 1;
                }
                return 0;
            }
        }

        public NoteType type;
        public int tick;
        public int lane;

        public override string ToString()
        {
            return $"tick={tick},type={type},weight={Weight},lane={lane}";
        }
    }

    public class ScoreNote
    {
        public float weight;
        public bool isfever;
        public readonly float frame;

        public ScoreNote(Note note)
        {
            weight = note.Weight;
            frame = note.frame;
        }
    }
}
