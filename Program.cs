using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace SekaiCalculator
{
    class Program
    {
        private const int skillframe = 300;

        private static float[] calcScore(string filename, MusicDifficulties diff)
        {
            int i;
            SusScoreData data;
            using (var reader = new StreamReader(File.OpenRead(filename)))
                data = new SusParser().Parse(reader);

            var beatdef = data.BpmDefinitions.Select(pair => new Tuple<int, float>(pair.Key, (float)pair.Value))
                .OrderBy(t => t.Item1).ToArray();
            Func<int, float> getFrame = tick =>
            {
                float s = 0;
                int n = beatdef.Length, j;
                for (j = 1; j < n; ++j)
                {
                    if (beatdef[j].Item1 > tick) break;
                    s += (beatdef[j].Item1 - beatdef[j - 1].Item1) / beatdef[j - 1].Item2;
                }
                return (s + (tick - beatdef[j - 1].Item1) / beatdef[j - 1].Item2) * 3600 / data.TicksPerBeat;
            };

            var flag = false;
            var lastfever = -1;

            var notes = data.ShortNotes.SelectMany(pair => pair.Value).Select(tuple =>
            {
                var frame = getFrame(tuple.Item2.Tick);
                if (tuple.Item2.LaneIndex == 0)
                    return new Note
                    {
                        frame = frame,
                        tick = tuple.Item2.Tick,
                        type = NoteType.Skill
                    };
                if (tuple.Item2.LaneIndex == 15)
                {
                    var flag2 = flag && lastfever != tuple.Item2.Tick;
                    flag = true;
                    lastfever = tuple.Item2.Tick;
                    return new Note
                    {
                        frame = frame,
                        tick = tuple.Item2.Tick,
                        type = flag2 ? NoteType.FeverStart : NoteType.FeverBegin
                    };
                }
                else
                    lastfever = -1;
                return new Note
                {
                    weight = tuple.Item1 - '0',
                    frame = frame,
                    tick = tuple.Item2.Tick,
                    type = NoteType.Normal
                };
            }).ToArray();

            var notes2 = new List<Note>();

            foreach (var l in data.LongNotes.SelectMany(pair => pair.Value))
            {
                var ln = l.Count;
                var lnote = l[ln - 1];
                int ftick = l[0].Item2.Tick, ltick = lnote.Item2.Tick;
                var flag2 = notes.Any(n => n.tick == ftick && n.weight == 2);

                if (flag2) notes = notes.Where(n => !(n.tick == ftick && n.weight == 2)).ToArray();

                notes2.Add(new Note
                {
                    weight = flag2 ? 2 : 1,
                    frame = getFrame(ftick),
                    tick = ftick,
                    type = NoteType.Normal
                });
                notes2.Add(new Note
                {
                    weight = !flag2 ? 1 : lnote.Item1 == '3' ? 3 : 2,
                    frame = getFrame(ltick),
                    tick = ltick,
                    type = NoteType.Normal
                });

                for (i = 1;i < ln - 1; ++i)
                {
                    var note = l[i];
                    if (note.Item1 != '3') continue;

                    notes2.Add(new Note
                    {
                        weight = flag2 ? 0.2f : 0.1f,
                        frame = getFrame(note.Item2.Tick),
                        tick = note.Item2.Tick,
                        type = NoteType.Normal
                    });
                }
                var tpb = data.TicksPerBeat / 2;

                for (i = (ftick / tpb + 1) * tpb; i < ltick; i += tpb)
                    notes2.Add(new Note
                    {
                        weight = 0.1f,
                        frame = getFrame(i),
                        tick = i,
                        type = NoteType.Normal
                    });
            } 

            notes = notes.Concat(notes2).OrderBy(n => n.tick).ToArray();
            var basescore = 4 / notes.Sum(n => n.weight) * (1 + (0.005f * (diff.playLevel - 5)));
            var skills = notes.Where(n => n.type == NoteType.Skill).Select(n => (int)(n.frame));
            var fevertick = notes.Single(n => n.type == NoteType.FeverStart).tick;
            var feverlast = diff.noteCount / 10;
            notes = notes.Where(n => n.type == NoteType.Normal).ToArray();

            for (i = 0; notes[i].tick < fevertick; ++i) ;
            for (; feverlast > 0; --feverlast)
                notes[i++].isfever = true;

            var nl = notes.Length;

            for (i = 0; i < nl; ++i)
                notes[i].weight *= ((i / 100) * 0.01f + 1);
            var rs = notes.Sum(n => (n.isfever ? 1.5f : 1) * n.weight) * basescore;
            var rss = notes.Sum(n => (n.isfever ? 2f : 1) * n.weight) * basescore;
            var sr = notes.Select(n => (n.isfever ? 1.5f : 1) * skills.Sum(s => s <= n.frame && s + skillframe > n.frame ? n.weight : 0)).Sum() * basescore;
            var srs = notes.Select(n => (n.isfever ? 1.5f : 1) * skills.Sum(s => s <= n.frame && s + skillframe > n.frame ? n.weight : 0)).Sum() * basescore;
            var le = notes.Max(n => n.frame / 60);
            return new float[] { rs, rss, sr, srs , le};
        }

        public static void Main(string[] args)
        {
            var master = JToken.Parse(File.ReadAllText("master_data.json"));
            var musics = master["musicAlls"].ToObject<Dictionary<string, Music>>().Values.ToArray();
            var metas = new List<MusicMeta>();

            foreach (var music in musics)
                foreach (var diff in music.musicDifficulties)
                {
                    var arr = calcScore($@"music_score\{music.id:d4}_01\{diff.musicDifficulty}.txt", diff);
                    metas.Add(new MusicMeta
                    {
                        difficulty = diff.musicDifficulty,
                        noteCount = diff.noteCount,
                        playLevel = diff.playLevel,
                        title = music.music.title,
                        relativeScore = arr[0],
                        relativeScoreS = arr[1],
                        skillReliance = arr[2],
                        skillRelianceS = arr[3],
                        length = (int)arr[4]
                    });
                }

            File.WriteAllText("music_meta.json", JsonConvert.SerializeObject(metas, Formatting.Indented));
            var str = "title,difficulty,length,note count,playlevel,relative score,skill reliance,relative score s,skill reliance s\n" +
                string.Join("\n", metas.Select(meta => $"{meta.title},{meta.difficulty},{meta.length},{meta.noteCount},{meta.playLevel},{meta.relativeScore},{meta.skillReliance},{meta.relativeScoreS},{meta.skillRelianceS}"));
            File.WriteAllText("music_meta.csv", str);
            File.WriteAllText("music_meta_gbk.csv", str, Encoding.GetEncoding("GBK"));
        }
    }
}
