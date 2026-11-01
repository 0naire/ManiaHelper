using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

internal sealed class StatsPreview:Control {
    readonly string[] labels={"MA","PR","GR","GD","BD","MS"};
    readonly Color[] tones={Color.FromArgb(127,231,228),Color.FromArgb(245,215,144),Color.FromArgb(153,213,178),Color.FromArgb(162,184,255),Color.FromArgb(207,172,243),Color.FromArgb(242,145,161)};
    int[] counts={2000,510,65,12,3,2};double accuracy=98.72,ur=45.2;
    int early=108,late=99,px=660,py=86,percent=110;bool live,shown=true,graphShown=true,playerShown=true;
    readonly int[] distribution=new int[41];
    readonly PictureBox avatar=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,ErrorImage=null,InitialImage=null};
    readonly PictureBox flag=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,ErrorImage=null,InitialImage=null};
    readonly PictureBox flag7=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,ErrorImage=null,InitialImage=null};
    OsuPlayerProfile player=new OsuPlayerProfile();
    public StatsPreview(){DoubleBuffered=true;Controls.Add(avatar);Controls.Add(flag);Controls.Add(flag7);DemoGraph();}
    void DemoGraph(){for(int i=0;i<41;i++)distribution[i]=(int)(18*Math.Exp(-Math.Pow((i-20)/5.5,2)));}
    public void SetPlacement(int x,int y,int size,bool enabled,bool graph=true,bool profile=true){px=x;py=y;percent=size;shown=enabled;graphShown=graph;playerShown=profile;avatar.Visible=profile;Invalidate();}
    public void SetPlayer(OsuPlayerProfile p){player=p;if(avatar.ImageLocation!=p.avatarUrl){avatar.CancelAsync();avatar.ImageLocation=p.avatarUrl;if(!String.IsNullOrEmpty(p.avatarUrl))try{avatar.LoadAsync();}catch{}}string flagUrl=System.Text.RegularExpressions.Regex.IsMatch(p.countryCode??"","^[A-Z]{2}$")?"https://osu.ppy.sh/images/flags/"+p.countryCode+".png":"";foreach(var picture in new[]{flag,flag7})if(picture.ImageLocation!=flagUrl){picture.CancelAsync();picture.ImageLocation=flagUrl;if(flagUrl.Length>0)try{picture.LoadAsync();}catch{}}Invalidate();}
    public void UseDemo(){live=false;counts=new[]{2000,510,65,12,3,2};accuracy=98.72;ur=45.2;early=108;late=99;DemoGraph();Invalidate();}
    static double Number(Dictionary<string,object> d,string key,double fallback=0){object v;double n;return d.TryGetValue(key,out v)&&double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?n:fallback;}
    public void SetPlay(Dictionary<string,object> play){
        accuracy=Math.Max(0,Math.Min(100,Number(play,"accuracy",100)));ur=Math.Max(0,Number(play,"unstableRate"));
        object raw;if(play.TryGetValue("hits",out raw)){var h=raw as Dictionary<string,object>;if(h!=null){string[] keys={"geki","300","katu","100","50","0"};for(int i=0;i<6;i++)counts[i]=(int)Math.Max(0,Number(h,keys[i]));}}
        live=true;Invalidate();
    }
    public void SetTimings(IList errors){early=0;late=0;Array.Clear(distribution,0,distribution.Length);foreach(object raw in errors){if(raw==null)continue;double v;try{v=Convert.ToDouble(raw);}catch{continue;}if(double.IsNaN(v)||double.IsInfinity(v))continue;if(v<0)early++;else if(v>0)late++;int bin=(int)Math.Floor((Math.Max(-150,Math.Min(150,v))/150+1)*20+.5);distribution[bin]++;}Invalidate();}
    void DrawText(Graphics g,string text,float size,Color color,Rectangle r,bool right=false){using(var f=new Font("Segoe UI",size))TextRenderer.DrawText(g,text,f,r,color,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|(right?TextFormatFlags.Right:TextFormatFlags.Left));}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        g.Clear(Parent!=null?Parent.BackColor:Color.FromArgb(18,19,25));
        Color muted=Color.FromArgb(156,163,186),white=Color.FromArgb(239,241,248);
        DrawText(g,(shown?(live?"LIVE":"예시"):"숨김")+" · "+percent+"%",8F,tones[0],new Rectangle(142,0,160,20),true);
        var contentState=g.Save();if(playerShown)g.TranslateTransform(0,110);
        DrawText(g,accuracy.ToString("F2",CultureInfo.InvariantCulture)+"%",29F,white,new Rectangle(35,23,265,48),true);
        DrawText(g,ur.ToString("F2",CultureInfo.InvariantCulture)+" UR",16F,white,new Rectangle(35,69,265,30),true);
        for(int i=0;i<6;i++){
            int yy=111+i*21;
            DrawText(g,labels[i],10F,tones[i],new Rectangle(55,yy,115,22));DrawText(g,counts[i].ToString("N0",CultureInfo.InvariantCulture),12F,white,new Rectangle(171,yy,129,22),true);
        }
        DrawText(g,"EARLY",9F,tones[0],new Rectangle(55,245,115,21));DrawText(g,early.ToString("N0",CultureInfo.InvariantCulture),12F,white,new Rectangle(171,245,129,21),true);
        DrawText(g,"LATE",9F,tones[5],new Rectangle(55,266,115,21));DrawText(g,late.ToString("N0",CultureInfo.InvariantCulture),12F,white,new Rectangle(171,266,129,21),true);
        int total=0;foreach(int count in counts)total+=count;
        string ratio=counts[1]>0?((double)counts[0]/counts[1]).ToString("F2",CultureInfo.InvariantCulture):counts[0]>0?"∞":"—";
        DrawText(g,"TOTAL",9F,muted,new Rectangle(55,287,115,21));DrawText(g,total.ToString("N0",CultureInfo.InvariantCulture),12F,white,new Rectangle(171,287,129,21),true);
        DrawText(g,"RATIO",9F,muted,new Rectangle(55,308,115,21));DrawText(g,ratio,12F,tones[2],new Rectangle(171,308,129,21),true);
        if(graphShown){
            DrawText(g,"TIMING DISTRIBUTION",8F,muted,new Rectangle(55,347,245,20));
            int peak=1;foreach(int v in distribution)peak=Math.Max(peak,v);
            for(int i=0;i<41;i++){float height=distribution[i]>0?Math.Max(1,distribution[i]*48F/peak):0;using(var b=new SolidBrush(i<20?tones[0]:i>20?tones[5]:white))g.FillRectangle(b,55+i*6,420-height,4,height);}
            using(var p=new Pen(Color.FromArgb(90,180,185,200),1))g.DrawLine(p,178,371,178,420);
            DrawText(g,"EARLY                 0                 LATE",7F,muted,new Rectangle(55,423,245,18));
        }
        g.Restore(contentState);
        int profileY=20;
        avatar.SetBounds(55,profileY,27,27);avatar.Visible=playerShown;
        flag.SetBounds(55,profileY+40,23,15);flag.Visible=playerShown&&!String.IsNullOrEmpty(flag.ImageLocation);
        flag7.SetBounds(55,profileY+61,23,15);flag7.Visible=playerShown&&!String.IsNullOrEmpty(flag7.ImageLocation);
        if(playerShown){
            DrawText(g,(player.countryRank4K.HasValue?"#"+player.countryRank4K.Value.ToString("N0"):"—")+" (4 Keys)",10F,white,new Rectangle(85,profileY+37,212,22));
            DrawText(g,(player.countryRank7K.HasValue?"#"+player.countryRank7K.Value.ToString("N0"):"—")+" (7 Keys)",10F,white,new Rectangle(85,profileY+58,212,22));
            DrawText(g,"P",14F,muted,new Rectangle(58,profileY-1,24,27));
            DrawText(g,player.name,10F,muted,new Rectangle(90,profileY,207,25));
        }
    }
}
