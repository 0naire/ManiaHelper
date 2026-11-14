using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

internal static class RankedMapsChecks {
    static void Check(bool condition, string name) {
        if (!condition) throw new InvalidOperationException(name);
    }

    static void Main() {
        const string latest = @"[
          {""id"":123,""status"":""ranked"",""title"":""A, B"",""artist"":""Artist"",
           ""ranked_date"":""2026-11-13T12:00:00Z"",""beatmaps"":[
             {""id"":10,""mode"":""mania"",""status"":""ranked"",""cs"":4,""version"":""4K Hard"",
              ""total_length"":91,""difficulty_rating"":4.24,""accuracy"":8.5},
             {""id"":11,""mode_int"":3,""cs"":7},
             {""id"":12,""mode"":""osu"",""cs"":4},
             {""id"":13,""mode"":""mania"",""convert"":true},
             {""id"":14,""mode"":""mania"",""status"":""loved""},
             {""id"":15,""mode"":""mania"",""lazer_only"":true}]},
          {""id"":456,""status"":""loved"",""beatmaps"":[{""id"":16,""mode"":""mania""}]}
        ]";
        var page = RankedMaps.ReadLatest(latest);
        Check(page.SetCount == 2 && page.Maps.Select(map => map.Id).SequenceEqual(new[] { 10, 11 }), "Native Ranked mania only");
        var first = page.Maps[0];
        Check(first.Keys == 4 && first.Seconds == 91 && first.Stars == 4.24 && first.RankedAt.HasValue, "Difficulty metadata");
        var pp = RankedMaps.ReadBatchPp(@"{""results"":{""10"":{""pp"":145.32},""11"":{""pp"":null},""12"":{""pp"":0}}}");
        Check(pp[10] == 145.32 && !pp.ContainsKey(11) && pp[12] == 0, "Missing PP is distinct from zero");

        const string sets = "art,t,bpm,s\r\n\"Artist, Duo\",\"Title \"\"Mix\"\"\",120,123";
        const string diffs = "m,b,pp99,v,s,l,d,appr_h,cs,accuracy,drain,dp\r\n" +
            "64,10,210.5,\"4K, Hard\",123,91,5.25,480000,4,8.5,7,\r\n" +
            "0,11,,7K,123,105,4.83,480000,7,8,6,1\r\n" +
            "0,12,100,Unranked,123,105,4.83,0,4,8,6,\r\n" +
            "64,10,210.5,Duplicate,123,91,5.25,480000,4,8.5,7,";
        var farm = RankedMaps.ReadFarmCsv(sets, diffs);
        Check(farm.Count == 2 && farm[0].Title == "Title \"Mix\"" && farm[0].Difficulty == "4K, Hard", "Quoted CSV and ranked filtering");
        Check(farm[0].Mods == "DT" && farm[0].Bpm == 180 && farm[0].Pp == 210.5 && !farm[1].Stars.HasValue, "Mod stats and pending stars");

        double? min, max;
        Check(RankedMapsForm.ReadRange("3.5-5", out min, out max) && min == 3.5 && max == 5, "Bounded range");
        Check(RankedMapsForm.ReadRange("-150", out min, out max) && !min.HasValue && max == 150, "Upper bound");
        Check(!RankedMapsForm.ReadRange("150 200", out min, out max) && !RankedMapsForm.ReadRange("5-3", out min, out max), "Invalid range");

        string archive = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".osz");
        try {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("Valid.osu").Open())) writer.Write("osu file format v14");
            RankedMaps.ValidateArchive(archive);
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) zip.CreateEntry("../bad.osu");
            bool rejected = false;
            try { RankedMaps.ValidateArchive(archive); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Unsafe archive paths are rejected");
        } finally {
            File.Delete(archive);
        }
        Console.WriteLine("Ranked maps checks passed.");
    }
}
