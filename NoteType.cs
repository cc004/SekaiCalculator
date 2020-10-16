using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SekaiCalculator
{
    [Flags]
    public enum NoteType
    {
        Normal = 0,
        FeverBegin = 1,
        FeverStart = 2,
        Skill = 3,
        Flick = 4,
        Critical = 8,
        Long = 16,
        Auto = 32,
        Mid = 64
    }
}
