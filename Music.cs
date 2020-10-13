using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SekaiCalculator
{
    [JsonObject]
    public class Music
    {
        public int id;
        public MusicInfo music;
        public MusicDifficulties[] musicDifficulties;
    }
}
