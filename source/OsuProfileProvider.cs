using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal sealed class OsuPlayerProfile {
    public int id;public string name="PLAYER",countryCode="",countryName="",avatarUrl="",status="",updatedAt="";
    public int? countryRank4K,countryRank7K;
}
internal static class OsuProfileProvider {
    static Dictionary<string,object> Map(object v){return v as Dictionary<string,object>;}
    static string Str(Dictionary<string,object> d,string k){object v;return d!=null&&d.TryGetValue(k,out v)&&v!=null?Convert.ToString(v):"";}
    public static OsuPlayerProfile Parse(string html,int expectedId){
        var serializer=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
        foreach(Match match in Regex.Matches(html,"data-initial-data=\"([^\"]*)\"",RegexOptions.Singleline)){
            var root=serializer.Deserialize<Dictionary<string,object>>(WebUtility.HtmlDecode(match.Groups[1].Value));
            object raw;if(!root.TryGetValue("user",out raw))continue;var user=Map(raw);int id;
            if(user==null||!int.TryParse(Str(user,"id"),out id)||id!=expectedId)continue;
            var p=new OsuPlayerProfile{id=id,name=Str(user,"username"),countryCode=Str(user,"country_code"),avatarUrl=Str(user,"avatar_url"),updatedAt=DateTime.UtcNow.ToString("o"),status="official"};
            if(user.TryGetValue("country",out raw)){var country=Map(raw);p.countryName=Str(country,"name");}
            if(user.TryGetValue("statistics",out raw)){
                var statistics=Map(raw);
                if(statistics!=null&&statistics.TryGetValue("variants",out raw)){
                    var variants=raw as IList;
                    if(variants!=null)foreach(object entry in variants){var variant=Map(entry);if(Str(variant,"mode")!="mania")continue;int rank;if(!int.TryParse(Str(variant,"country_rank"),out rank)||rank<=0)continue;string key=Str(variant,"variant");if(key=="4k")p.countryRank4K=rank;else if(key=="7k")p.countryRank7K=rank;}
                }
            }
            Uri avatar;if(!Uri.TryCreate(p.avatarUrl,UriKind.Absolute,out avatar)||avatar.Scheme!="https"||avatar.Host!="a.ppy.sh")p.avatarUrl="";
            return p;
        }
        throw new FormatException("Official user profile data was unavailable.");
    }
    public static async Task<OsuPlayerProfile> FetchAsync(int id){
        if(id<=0)throw new ArgumentOutOfRangeException("id");
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        using(var client=new WebClient()){
            client.Encoding=System.Text.Encoding.UTF8;client.Headers[HttpRequestHeader.UserAgent]="ManiaHelper/1.3 (+https://github.com/0naire/ManiaHelper)";
            var operation=client.DownloadStringTaskAsync(new Uri("https://osu.ppy.sh/users/"+id+"/mania"));
            if(await Task.WhenAny(operation,Task.Delay(10000))!=operation){client.CancelAsync();throw new TimeoutException("Profile lookup timed out.");}
            return Parse(await operation,id);
        }
    }
}
