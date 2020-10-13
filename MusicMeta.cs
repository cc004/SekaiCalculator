using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SekaiCalculator
{
    [JsonObject]
    public class MusicMeta
    {
        public string title;
        public int playLevel;
        public int noteCount;
        public string difficulty;
        public float relativeScore;
        public float relativeScoreS;
        public float skillReliance;
        public float skillRelianceS;
        public int length;
    }
}
