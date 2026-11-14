using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;

internal sealed class RankedMap {
    public int Id, SetId, Keys, Seconds;
    public string Title, Artist, Difficulty, Mapper, Mods;
    public double? Pp, Stars, Bpm, Od, Hp;
    public DateTimeOffset? RankedAt;
    public string SearchText { get { return Title + " " + Artist + " " + Difficulty + " " + Mapper + " " + Id + " " + SetId; } }
}

internal sealed class RankedMapPage {
    public readonly List<RankedMap> Maps = new List<RankedMap>();
    public int SetCount;
}

internal static class RankedMaps {
    const string Mirror = "https://mirror.hinamizawa.ai";
    const int MaxMapBytes = 512 * 1024 * 1024;
    public const int SetsPerPage = 25;
    static readonly HttpClient http = CreateClient();

    static HttpClient CreateClient() {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var client = new HttpClient(new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        client.Timeout = TimeSpan.FromSeconds(25);
        client.MaxResponseContentBufferSize = 16 * 1024 * 1024;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ManiaHelper/1.5 (+https://github.com/0naire/ManiaHelper)");
        return client;
    }

    static async Task<string> GetText(string url, CancellationToken token) {
        using (var response = await http.GetAsync(url, token).ConfigureAwait(false)) {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }

    public static Task<RankedMapPage> GetLatestSets(string query, int offset, CancellationToken token) {
        string url = Mirror + "/api/v2/search?mode=3&status=ranked&sort=ranked_date%3Adesc&amount=" +
            SetsPerPage + "&offset=" + offset + "&q=" + Uri.EscapeDataString(query);
        return GetLatestSets(url, token);
    }

    static async Task<RankedMapPage> GetLatestSets(string url, CancellationToken token) {
        return ReadLatest(await GetText(url, token).ConfigureAwait(false));
    }

    internal static RankedMapPage ReadLatest(string json) {
        var sets = new JavaScriptSerializer().DeserializeObject(json) as IList;
        if (sets == null) throw new InvalidDataException("Invalid beatmap list.");
        var page = new RankedMapPage { SetCount = sets.Count };
        foreach (var raw in sets) {
            var set = raw as Dictionary<string, object>;
            if (set == null || !IsRanked(set)) continue;
            int setId = Int(Value(set, "id"));
            var difficulties = Value(set, "beatmaps") as IList;
            if (setId <= 0 || difficulties == null) continue;
            foreach (var entry in difficulties) {
                var diff = entry as Dictionary<string, object>;
                if (diff == null || (Text(Value(diff, "mode")) != "mania" &&
                        Int(Value(diff, "mode")) != 3 && Int(Value(diff, "mode_int")) != 3) ||
                    IsTrue(Value(diff, "convert")) || IsTrue(Value(diff, "lazer_only"))) continue;
                if ((Value(diff, "status") != null || Value(diff, "ranked") != null) && !IsRanked(diff)) continue;
                int id = Int(Value(diff, "id"));
                if (id <= 0) continue;
                page.Maps.Add(new RankedMap {
                    Id = id, SetId = setId, Title = Text(Value(set, "title")), Artist = Text(Value(set, "artist")),
                    Difficulty = Text(Value(diff, "version")), Mapper = Text(Value(set, "creator")), Mods = "NM",
                    Keys = Int(Value(diff, "cs")), Seconds = Int(Value(diff, "total_length")),
                    Stars = Number(Value(diff, "difficulty_rating")), Bpm = Number(Value(diff, "bpm")) ?? Number(Value(set, "bpm")),
                    Od = Number(Value(diff, "accuracy")), Hp = Number(Value(diff, "drain")), RankedAt = Date(Text(Value(set, "ranked_date")))
                });
            }
        }
        return page;
    }

    public static async Task<Dictionary<int, double>> GetLatestPp(IEnumerable<int> ids, CancellationToken token) {
        var remaining = ids.Distinct().ToArray();
        var result = new Dictionary<int, double>();
        for (int offset = 0; offset < remaining.Length; offset += 100) {
            var batch = remaining.Skip(offset).Take(100);
            string url = Mirror + "/v3/osu/pp/batch?mode=3&mods=NM&ids=" + String.Join(",", batch);
            foreach (var pp in ReadBatchPp(await GetText(url, token).ConfigureAwait(false))) result[pp.Key] = pp.Value;
        }
        return result;
    }

    internal static Dictionary<int, double> ReadBatchPp(string json) {
        var envelope = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        var entries = Value(envelope, "results") as Dictionary<string, object>;
        if (entries == null) throw new InvalidDataException("Invalid PP response.");
        var result = new Dictionary<int, double>();
        foreach (var entry in entries) {
            var row = entry.Value as Dictionary<string, object>;
            double? pp = Number(Value(row, "pp"));
            int id = Int(entry.Key);
            if (id > 0 && pp.HasValue && pp >= 0) result[id] = pp.Value;
        }
        return result;
    }

    public static async Task<List<RankedMap>> GetFarmMaps(CancellationToken token) {
        const string published = "https://data.osu-pps.com/";
        const string fallback = "https://raw.githubusercontent.com/grumd/osu-pps/data/data/";
        try { return await ReadFarmSource(published, token).ConfigureAwait(false); }
        catch (Exception error) {
            token.ThrowIfCancellationRequested();
            if (!(error is HttpRequestException || error is InvalidDataException || error is TaskCanceledException ||
                    error is MalformedLineException)) throw;
        }
        return await ReadFarmSource(fallback, token).ConfigureAwait(false);
    }

    static async Task<List<RankedMap>> ReadFarmSource(string root, CancellationToken token) {
        const string path = "maps/mania/";
        var parts = await Task.WhenAll(GetText(root + path + "mapsets.csv", token), GetText(root + path + "diffs.csv", token)).ConfigureAwait(false);
        return await Task.Run(() => ReadFarmCsv(parts[0], parts[1]), token).ConfigureAwait(false);
    }

    internal static List<RankedMap> ReadFarmCsv(string mapsets, string difficulties) {
        var sets = new Dictionary<int, string[]>();
        using (var csv = OpenCsv(mapsets)) {
            var header = ReadHeader(csv);
            while (!csv.EndOfData) {
                var row = csv.ReadFields();
                int id = Int(Field(header, row, "s", "mapsetId"));
                if (id > 0) sets[id] = new[] { Field(header, row, "art", "artist"), Field(header, row, "t", "title"), Field(header, row, "bpm", "bpm") };
            }
        }
        var maps = new List<RankedMap>();
        var seen = new HashSet<Tuple<int, int>>();
        using (var csv = OpenCsv(difficulties)) {
            var header = ReadHeader(csv);
            while (!csv.EndOfData) {
                var row = csv.ReadFields();
                int id = Int(Field(header, row, "b", "beatmapId"));
                int setId = Int(Field(header, row, "s", "mapsetId"));
                int mods = Int(Field(header, row, "m", "mods"));
                int keys = Int(Field(header, row, "cs", "maniaKeys"));
                int rankedHours = Int(Field(header, row, "appr_h", "approvedHoursTimestamp"));
                string[] set;
                if (id <= 0 || !sets.TryGetValue(setId, out set) || keys < 1 || keys > 18 ||
                    rankedHours <= 0 || rankedHours > 5000000 || !seen.Add(Tuple.Create(id, mods))) continue;
                maps.Add(new RankedMap {
                    Id = id, SetId = setId, Title = set[1], Artist = set[0], Mods = ModName(mods), Keys = keys,
                    Difficulty = Field(header, row, "v", "version"), Seconds = Int(Field(header, row, "l", "length")),
                    Stars = Int(Field(header, row, "dp", "difficultyPending")) == 1 ||
                        IsTrue(Field(header, row, "dp", "difficultyPending")) ? null : Number(Field(header, row, "d", "difficulty")),
                    Pp = Number(Field(header, row, "pp99", "pp")), Bpm = Number(set[2]) * Speed(mods),
                    Od = Number(Field(header, row, "accuracy", "accuracy")), Hp = Number(Field(header, row, "drain", "drain")),
                    RankedAt = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddHours(rankedHours)
                });
            }
        }
        if (maps.Count == 0) throw new InvalidDataException("Invalid osu-pps catalog.");
        return maps;
    }

    static TextFieldParser OpenCsv(string text) {
        var csv = new TextFieldParser(new StringReader(text));
        csv.SetDelimiters(",");
        csv.TrimWhiteSpace = false;
        return csv;
    }
    static Dictionary<string, int> ReadHeader(TextFieldParser csv) {
        if (csv.EndOfData) throw new InvalidDataException("Empty osu-pps data.");
        return csv.ReadFields().Select((name, index) => new { name, index }).ToDictionary(p => p.name.TrimStart('\uFEFF'), p => p.index);
    }
    static string Field(Dictionary<string, int> header, string[] row, string legacy, string current) {
        int index;
        return (header.TryGetValue(current, out index) || header.TryGetValue(legacy, out index)) && index < row.Length ? row[index] : "";
    }
    static double Speed(int mods) { return (mods & 64) != 0 ? 1.5 : (mods & 256) != 0 ? 0.75 : 1; }
    static string ModName(int mods) {
        if (mods == 0) return "NM";
        string name = ((mods & 2) != 0 ? "EZ" : "") + ((mods & 64) != 0 ? "DT" : "") + ((mods & 256) != 0 ? "HT" : "");
        return (mods & ~(2 | 64 | 256)) == 0 ? name : mods.ToString(CultureInfo.InvariantCulture);
    }
    static object Value(Dictionary<string, object> row, string key) {
        object value;
        return row != null && row.TryGetValue(key, out value) ? value : null;
    }
    static string Text(object value) { return Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; }
    static int Int(object value) {
        int result;
        return Int32.TryParse(Text(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
    }
    static double? Number(object value) {
        double result;
        return Double.TryParse(Text(value), NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            !Double.IsNaN(result) && !Double.IsInfinity(result) ? (double?)result : null;
    }
    static bool IsTrue(object value) { return String.Equals(Text(value), "true", StringComparison.OrdinalIgnoreCase); }
    static bool IsRanked(Dictionary<string, object> row) {
        return Text(Value(row, "status")) == "ranked" || Int(Value(row, "status")) == 1 || Int(Value(row, "ranked")) == 1;
    }
    static DateTimeOffset? Date(string text) {
        DateTimeOffset date;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date) ? (DateTimeOffset?)date : null;
    }

    public static async Task<string> Download(int setId, IProgress<int?> progress, CancellationToken token) {
        if (setId <= 0) throw new ArgumentOutOfRangeException("setId");
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "0naire", "ManiaHelper", "Beatmaps");
        Directory.CreateDirectory(dir);
        string destination = Path.Combine(dir, setId + ".osz");
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(TimeSpan.FromMinutes(4));
                string url = Mirror + "/api/v1/hinai/d/" + setId + "n";
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false)) {
                    response.EnsureSuccessStatusCode();
                    long? length = response.Content.Headers.ContentLength;
                    if (length > MaxMapBytes) throw new InvalidDataException("비트맵 파일이 너무 큽니다.");
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (timeout.Token.Register(() => stream.Dispose()))
                    using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) {
                        var buffer = new byte[65536];
                        long total = 0;
                        int previous = -1;
                        while (true) {
                            int count = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
                            if (count == 0) break;
                            total += count;
                            if (total > MaxMapBytes) throw new InvalidDataException("비트맵 파일이 너무 큽니다.");
                            await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                            if (length > 0) {
                                int percentage = (int)Math.Min(100, total * 100 / length.Value);
                                if (percentage != previous) progress.Report(previous = percentage);
                            }
                        }
                    }
                }
                await Task.Run(() => ValidateArchive(partial), timeout.Token).ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(partial, destination, null);
                else File.Move(partial, destination);
                return destination;
            }
        } finally {
            try { File.Delete(partial); } catch (IOException) { }
        }
    }

    internal static void ValidateArchive(string file) {
        using (var zip = ZipFile.OpenRead(file)) {
            bool hasMap = false;
            long expanded = 0;
            foreach (var entry in zip.Entries) {
                string name = entry.FullName.Replace('\\', '/');
                expanded += entry.Length;
                if (name.StartsWith("/") || name.IndexOf(':') >= 0 ||
                    name.Split('/').Any(part => part.TrimEnd(' ') == "..") ||
                    expanded > 2L * MaxMapBytes)
                    throw new InvalidDataException("올바르지 않은 비트맵 압축 파일입니다.");
                if (name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase) && entry.Length > 0) hasMap = true;
            }
            if (!hasMap) throw new InvalidDataException("압축 파일에 .osu 맵이 없습니다.");
        }
    }
}
