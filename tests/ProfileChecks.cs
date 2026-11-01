using System;
using System.IO;
using System.Net;
internal static class ProfileChecks {
    static void Main(string[] args){
        string user="{\"user\":{\"id\":42,\"username\":\"EXAMPLE\",\"country_code\":\"KR\",\"country\":{\"name\":\"South Korea\"},\"avatar_url\":\"https://a.ppy.sh/42\",\"statistics\":{\"country_rank\":999,\"variants\":[{\"mode\":\"mania\",\"variant\":\"7k\",\"country_rank\":22},{\"mode\":\"mania\",\"variant\":\"4k\",\"country_rank\":7}]}}}";
        var p=OsuProfileProvider.Parse("<div data-initial-data=\""+WebUtility.HtmlEncode(user)+"\"></div>",42);
        if(p.countryRank4K!=7||p.countryRank7K!=22||p.countryCode!="KR"||p.name!="EXAMPLE")throw new Exception("Variant mapping failed");
        string noVariant=user.Replace("\"variant\":\"4k\"","\"variant\":\"unknown\"");
        if(OsuProfileProvider.Parse("<div data-initial-data=\""+WebUtility.HtmlEncode(noVariant)+"\"></div>",42).countryRank4K.HasValue)throw new Exception("Must not use combined rank");
        bool rejected=false;try{OsuProfileProvider.Parse("<div data-initial-data=\""+WebUtility.HtmlEncode(user)+"\"></div>",123);}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("Wrong user accepted");
        Console.WriteLine("PASS: separate 4K/7K country ranks, country, identity match and no combined-rank fallback");
        if(args.Length>0){var actual=OsuProfileProvider.Parse(File.ReadAllText(args[0]),int.Parse(args[1]));Console.WriteLine("Verified official profile: "+actual.name+" / "+actual.countryCode+" / 4K #"+actual.countryRank4K);}
    }
}
