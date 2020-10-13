using Newtonsoft.Json;

namespace SekaiCalculator
{
    [JsonObject]
    public class MusicDifficulties
    {
        public string musicDifficulty;
        public int playLevel;
        public int noteCount;
    }
}