using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
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
            var notehash = new HashSet<Tuple<char, NotePosition>>();

            var notes = data.ShortNotes['1'].Select(tuple =>
            {
                if (notehash.Contains(tuple))
                {
                    Console.WriteLine($"omitting duplicated short note for score: {filename}");
                    return null;
                }
                notehash.Add(tuple);
                var frame = getFrame(tuple.Item2.Tick);
                if (tuple.Item2.LaneIndex == 0)
                    return new Note
                    {
                        lane = tuple.GetHashCode(),
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
                        lane = tuple.GetHashCode(),
                        frame = frame,
                        tick = tuple.Item2.Tick,
                        type = flag2 ? NoteType.FeverStart : NoteType.FeverBegin
                    };
                }
                else
                    lastfever = -1;
                return new Note
                {
                    lane = tuple.Item2.LaneIndex,
                    frame = frame,
                    tick = tuple.Item2.Tick,
                    type = (tuple.Item1 == '2' ? NoteType.Critical : NoteType.Normal) |
                           (data.ShortNotes['5'].Any(t => t.Item2.Tick == tuple.Item2.Tick && t.Item2.LaneIndex == tuple.Item2.LaneIndex) ? NoteType.Flick : NoteType.Normal)
                };
            }).Where(n => n != null).ToArray();

            var notedict = new Dictionary<Tuple<int, int>, Note>();
            var fnoteset = new HashSet<Tuple<char, NotePosition>>();

            foreach (var note in notes)
            {
                var key = new Tuple<int, int>(note.tick, note.lane);
                notedict.Add(key, note);
            }

            foreach (var l in data.LongNotes.SelectMany(pair => pair.Value))
            {
                var ln = l.Count;
                var lnote = l[ln - 1];
                var fnote = l[0];
                if (fnoteset.Contains(fnote))
                {
                    Console.WriteLine($"omitting duplicated long note for score: {filename}");
                    continue;
                }
                fnoteset.Add(fnote);
                var fattr = NoteType.Normal;
                if (notedict.TryGetValue(new Tuple<int, int>(fnote.Item2.Tick, fnote.Item2.LaneIndex), out var note3))
                    fattr = note3.type;

                var lattr = NoteType.Normal;
                var lkey = new Tuple<int, int>(lnote.Item2.Tick, lnote.Item2.LaneIndex);

                if (notedict.TryGetValue(lkey, out var note2))
                {
                    lattr = note2.type;
                    notedict.Remove(lkey);
                }

                notedict.Add(lkey, new Note
                {
                    lane = lnote.Item2.LaneIndex,
                    frame = getFrame(lnote.Item2.Tick),
                    tick = lnote.Item2.Tick,
                    type = (fattr & NoteType.Critical) | lattr |
                        (data.ShortNotes['5'].Any(t => t.Item2.Tick == lnote.Item2.Tick && t.Item2.LaneIndex == lnote.Item2.LaneIndex) ? NoteType.Flick : NoteType.Normal)
                });

                for (i = 0;i < ln - 1; ++i)
                {
                    var note = l[i];
                    var key = new Tuple<int, int>(note.Item2.Tick, note.Item2.LaneIndex);

                    if (notedict.ContainsKey(key)) notedict.Remove(key);
                    if (note.Item1 == '5') continue;

                    notedict.Add(key, new Note
                    {
                        lane = note.Item2.LaneIndex,
                        frame = getFrame(note.Item2.Tick),
                        tick = note.Item2.Tick,
                        type = (fattr & NoteType.Critical) | (i == 0 ? NoteType.Long : NoteType.Mid)
                    });
                }

                var tpb = data.TicksPerBeat / 2;

                for (i = (fnote.Item2.Tick / tpb + 1) * tpb; i < lnote.Item2.Tick; i += tpb)
                    notedict.Add(new Tuple<int, int>(i, fnote.GetHashCode()), new Note
                    {
                        frame = getFrame(i),
                        tick = i,
                        type = NoteType.Auto
                    });
            } 
            /*
            Console.Clear();
            Console.WriteLine(filename);
            int combo = 0;
            foreach (var note in notedict.Select(n => n.Value).OrderBy(n => n.tick))
            {
                Console.WriteLine($"tick={note.tick},type={note.type},lane={note.lane},combo={combo += ((note.type & NoteType.Skill) == 0 ? 1 : 0 )}");
            }
            */
            notes = notedict.Select(pair => pair.Value).ToArray();


            var basescore = 4 / notes.Sum(n => n.Weight) * (1 + (0.005f * (diff.playLevel - 5)));
            var skills = notes.Where(n => n.type == NoteType.Skill).Select(n => (int)(n.frame));
            var fevertick = notes.Single(n => n.type == NoteType.FeverStart).tick;
            var feverlast = diff.noteCount / 10;
            var snotes = notes.Where(n => (n.type & NoteType.Skill) == 0).Select(n => new ScoreNote(n)).ToArray();
            if (snotes.Length != diff.noteCount) throw new Exception();

            for (i = 0; notes[i].tick < fevertick; ++i) ;
            for (; feverlast > 0; --feverlast)
                snotes[i++].isfever = true;

            var nl = snotes.Length;

            for (i = 0; i < nl; ++i)
                snotes[i].weight *= ((i / 100) * 0.01f + 1);

            var rs = snotes.Sum(n => (n.isfever ? 1.5f : 1) * n.weight) * basescore;
            var rss = snotes.Sum(n => (n.isfever ? 2f : 1) * n.weight) * basescore;
            var sr = snotes.Select(n => (n.isfever ? 1.5f : 1) * skills.Sum(s => s <= n.frame && s + skillframe > n.frame ? n.weight : 0)).Sum() * basescore;
            var srs = snotes.Select(n => (n.isfever ? 1.5f : 1) * skills.Sum(s => s <= n.frame && s + skillframe > n.frame ? n.weight : 0)).Sum() * basescore;
            var le = snotes.Max(n => n.frame / 60);
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
                    try
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
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

            File.WriteAllText("music_meta.json", JsonConvert.SerializeObject(metas, Formatting.Indented));
            var str = "title,difficulty,length,note count,playlevel,relative score,skill reliance,relative score s,skill reliance s\n" +
                string.Join("\n", metas.Select(meta => $"{meta.title},{meta.difficulty},{meta.length},{meta.noteCount},{meta.playLevel},{meta.relativeScore},{meta.skillReliance},{meta.relativeScoreS},{meta.skillRelianceS}"));
            File.WriteAllText("music_meta.csv", str);
            File.WriteAllText("music_meta_gbk.csv", str, Encoding.GetEncoding("GBK"));
        }
    }
}
