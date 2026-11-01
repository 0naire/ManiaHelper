using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

internal sealed class StatsPreview:Control {
    readonly string[] labels={"MAX","PERFECT","GREAT","GOOD","BAD","MISS"};
    readonly Color[] tones={Color.FromArgb(127,231,228),Color.FromArgb(245,215,144),Color.FromArgb(153,213,178),Color.FromArgb(162,184,255),Color.FromArgb(207,172,243),Color.FromArgb(242,145,161)};
    int[] counts={2000,510,65,12,3,2};double accuracy=98.72,ur=45.2;
    int early=108,late=99,px=270,py=115,percent=90;bool live,shown=true;
    public StatsPreview(){DoubleBuffered=true;}
    public void SetPlacement(int x,int y,int size,bool enabled){px=x;py=y;percent=size;shown=enabled;Invalidate();}
    public void UseDemo(){live=false;counts=new[]{2000,510,65,12,3,2};accuracy=98.72;ur=45.2;early=108;late=99;Invalidate();}
    static double Number(Dictionary<string,object> d,string key,double fallback=0){object v;double n;return d.TryGetValue(key,out v)&&double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?n:fallback;}
    public void SetPlay(Dictionary<string,object> play){
        accuracy=Math.Max(0,Math.Min(100,Number(play,"accuracy",100)));ur=Math.Max(0,Number(play,"unstableRate"));
        object raw;if(play.TryGetValue("hits",out raw)){var h=raw as Dictionary<string,object>;if(h!=null){string[] keys={"geki","300","katu","100","50","0"};for(int i=0;i<6;i++)counts[i]=(int)Math.Max(0,Number(h,keys[i]));}}
        live=true;Invalidate();
    }
    public void SetTimings(IList errors){early=0;late=0;foreach(object raw in errors){if(raw==null)continue;double v;try{v=Convert.ToDouble(raw);}catch{continue;}if(double.IsNaN(v)||double.IsInfinity(v))continue;if(v<0)early++;else if(v>0)late++;}Invalidate();}
    void DrawText(Graphics g,string text,float size,Color color,Rectangle r,bool right=false){using(var f=new Font("Segoe UI",size))TextRenderer.DrawText(g,text,f,r,color,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|(right?TextFormatFlags.Right:TextFormatFlags.Left));}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=SoftButton.Rounded(new RectangleF(0,0,Width-1,Height-1),16))using(var b=new SolidBrush(Color.FromArgb(27,29,42)))g.FillPath(b,path);
        Color muted=Color.FromArgb(156,163,186),white=Color.FromArgb(239,241,248);
        DrawText(g,"PERFORMANCE",8F,muted,new Rectangle(17,12,150,20));
        DrawText(g,(shown?(live?"LIVE":"예시"):"숨김")+" · "+percent+"%",8F,tones[0],new Rectangle(192,12,110,20),true);
        DrawText(g,accuracy.ToString("F2",CultureInfo.InvariantCulture)+"%",29F,white,new Rectangle(13,32,295,52));
        DrawText(g,"UNSTABLE RATE",8F,muted,new Rectangle(18,83,160,22));
        DrawText(g,ur.ToString("F2",CultureInfo.InvariantCulture)+" UR",12F,white,new Rectangle(170,80,132,26),true);
        for(int i=0;i<6;i++){
            int yy=116+i*23;
            using(var b=new SolidBrush(tones[i]))g.FillRectangle(b,19,yy+4,2,15);
            DrawText(g,labels[i],9F,tones[i],new Rectangle(29,yy,147,22));DrawText(g,counts[i].ToString("N0",CultureInfo.InvariantCulture),11F,white,new Rectangle(171,yy,129,22),true);
        }
        using(var p=new Pen(Color.FromArgb(55,60,76)))g.DrawLine(p,19,261,300,261);
        DrawText(g,"EARLY "+early.ToString("N0",CultureInfo.InvariantCulture),9F,tones[0],new Rectangle(17,268,148,23));
        DrawText(g,"LATE "+late.ToString("N0",CultureInfo.InvariantCulture),9F,tones[5],new Rectangle(166,268,136,23),true);
        int total=0;foreach(int count in counts)total+=count;
        string ratio=counts[1]>0?((double)counts[0]/counts[1]).ToString("F2",CultureInfo.InvariantCulture):counts[0]>0?"∞":"—";
        DrawText(g,"TOTAL "+total.ToString("N0",CultureInfo.InvariantCulture),8F,muted,new Rectangle(17,295,148,20));
        DrawText(g,"MAX / PR  "+ratio,8F,muted,new Rectangle(166,295,136,20),true);
        DrawText(g,"X "+px+"  ·  Y "+py,7F,muted,new Rectangle(17,314,285,15));
    }
}
