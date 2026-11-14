using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class Entry {
    [STAThread] static int Main(string[] args) {
        if (Array.IndexOf(args, "--register-auto-start") >= 0) {
            try { AutoStart.RegisterFromSetup(); return 0; }
            catch { return 1; }
        }
        bool watching = Array.IndexOf(args, "--watch") >= 0;
        if (watching && !AutoStart.IsEnabled) return 0;
        bool created;
        using (var mutex = new Mutex(true, "Local\\0naireManiaHelper", out created)) {
            if (!created) {
                string requestName = watching ? TimingForm.ReloadAutoStartEvent : TimingForm.ShowSettingsEvent;
                try { using (var request = EventWaitHandle.OpenExisting(requestName)) request.Set(); }
                catch (WaitHandleCannotBeOpenedException) { }
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new TimingForm(Array.IndexOf(args, "--start") >= 0))
            using (var context = new ApplicationContext()) {
                form.FormClosed += delegate { context.ExitThread(); };
                if (watching) form.CreateHandleForWatcher();
                else form.Show();
                Application.Run(context);
            }
        }
        return 0;
    }
}

internal sealed class TimingForm : Form {
    public const string ShowSettingsEvent = "Local\\0naireManiaHelperShowSettings";
    public const string ReloadAutoStartEvent = "Local\\0naireManiaHelperReloadAutoStart";
    readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    readonly Button start = new SoftButton(), stop = new SoftButton(), fit = new SoftButton();
    readonly Label status = new Label(), skinLabel = new Label();
    readonly NumericUpDown x = new NumericUpDown(), y = new NumericUpDown(), width = new NumericUpDown(), size = new NumericUpDown();
    readonly NumericUpDown statsX=new NumericUpDown(),statsY=new NumericUpDown(),statsSize=new NumericUpDown();
    readonly CheckBox statsEnabled=new CheckBox();
    readonly CheckBox graphEnabled=new CheckBox(),playerEnabled=new CheckBox();
    readonly StatsPreview statsPreview=new StatsPreview();
    readonly TracePreview trace = new TracePreview();
    readonly CheckBox preview = new CheckBox();
    readonly CheckBox autoStart = new CheckBox();
    readonly NotifyIcon tray=new NotifyIcon();
    RankedMapsForm rankedMaps;
    readonly EventWaitHandle showSettingsRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEvent);
    readonly EventWaitHandle reloadAutoStartRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ReloadAutoStartEvent);
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer previewTimer = new System.Windows.Forms.Timer();
    readonly ConnectionRetry connectionRetry = new ConnectionRetry();
    Process backend;
    ChildJob job;
    bool loading, stopping, polling, closing, starting, previewPolling;
    bool startedAutomatically, hasGameData;
    int handledGameId, automaticGameId;
    readonly int sessionId;
    double comboPosition=121;
    int currentProfileId;bool profilePolling;DateTime lastProfileFetch=DateTime.MinValue;Task playerTask;
    string skin = "—", gamePath = "", connectedGame, sizedFor;
    string Runtime { get { return Path.Combine(root, "runtime"); } }
    string LayoutPath { get { return Path.Combine(Runtime, "static", "ZeroTimingBar", "layout.json"); } }
    public TimingForm(bool startDirect=false) {
        using (var current = Process.GetCurrentProcess()) sessionId = current.SessionId;
        Text = "Mania Helper";
        ClientSize = new Size(1000, 596);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Malgun Gothic", 10F);
        DoubleBuffered = true;
        BackColor = Color.FromArgb(18, 19, 25);
        ForeColor = Color.FromArgb(240, 240, 245);
        AddLabel("타이밍 바", 24, 20, 180, 29, 13F);
        skinLabel.SetBounds(195, 22, 429, 24); skinLabel.TextAlign=ContentAlignment.MiddleRight;
        skinLabel.AutoEllipsis = true;
        skinLabel.Font=new Font("Malgun Gothic",8.5F); skinLabel.ForeColor=Color.FromArgb(157,159,176); Controls.Add(skinLabel);
        trace.SetBounds(24, 56, 600, 150); Controls.Add(trace);
        ConfigureNumber(x, "가로 위치", 24, 224, -427, 1281, 141);
        ConfigureNumber(y, "세로 위치", 177, 224, 0, 480, 141);
        ConfigureNumber(width, "눈금 범위", 330, 224, 40, 200, 141);
        ConfigureNumber(size, "크기 (%)", 483, 224, 50, 220, 141);
        preview.Text = "게임 메뉴에서 미리보기"; preview.SetBounds(24, 302, 260, 29);
        preview.CheckedChanged += delegate { if (!loading) SaveLayout(backend != null); };
        Controls.Add(preview);
        var chooseGame = new SoftButton();
        SetButton(chooseGame, "osu! 위치 선택", 303, 298, 139, 36);
        chooseGame.Click += delegate {
            if (ChooseGame() && backend == null) status.Text = "osu! 위치 저장됨";
        };
        SetButton(fit, "스킨에 맞추기", 454, 298, 170, 36);
        fit.Click += delegate { FitToSkin(); };
        var browseMaps = new SoftButton();
        SetButton(browseMaps, "매니아 랭크맵", 24, 351, 170, 36);
        browseMaps.ForeColor = Color.FromArgb(234, 165, 192);
        browseMaps.Click += delegate { ShowRankedMaps(); };
        AddLabel("판정 HUD", 656, 20, 320, 29, 13F);
        var previewScroll=new Panel{AutoScroll=true};previewScroll.SetBounds(656,56,320,340);Controls.Add(previewScroll);
        statsPreview.SetBounds(0,0,300,660);previewScroll.Controls.Add(statsPreview);
        statsEnabled.Text="판정 표시";statsEnabled.SetBounds(656,406,112,28);statsEnabled.Checked=true;
        statsEnabled.CheckedChanged+=delegate{if(!loading)SaveLayout(backend!=null);};Controls.Add(statsEnabled);
        graphEnabled.Text="그래프";graphEnabled.SetBounds(772,406,90,28);graphEnabled.Checked=true;graphEnabled.CheckedChanged+=delegate{if(!loading)SaveLayout(backend!=null);};Controls.Add(graphEnabled);
        playerEnabled.Text="프로필";playerEnabled.SetBounds(884,406,92,28);playerEnabled.Checked=true;playerEnabled.CheckedChanged+=delegate{if(!loading)SaveLayout(backend!=null);};Controls.Add(playerEnabled);
        ConfigureNumber(statsX,"가로 위치",656,448,-427,1281,100);
        ConfigureNumber(statsY,"세로 위치",766,448,0,480,100);
        ConfigureNumber(statsSize,"크기 (%)",876,448,50,180,100);
        autoStart.Text = "osu! 실행 시 자동 시작";
        autoStart.SetBounds(24, 405, 350, 30);
        autoStart.Checked = AutoStart.IsEnabled;
        autoStart.Click += delegate {
            try {
                AutoStart.SetEnabled(autoStart.Checked);
                handledGameId = 0;
                if (backend == null) status.Text = autoStart.Checked ? "osu! 실행 대기" : "대기 중";
            } catch (Exception e) {
                autoStart.Checked = AutoStart.IsEnabled;
                status.Text = "자동 시작 설정 실패";
                MessageBox.Show(this, e.Message, "자동 시작");
            }
        };
        Controls.Add(autoStart);
        SetButton(start, "시작", 24, 448, 294, 60);
        start.BackColor = Color.FromArgb(232, 153, 183); start.ForeColor = Color.FromArgb(27, 24, 34);
        start.Click += async delegate { await StartBackend(); };
        SetButton(stop, "중지", 330, 448, 294, 60); stop.Enabled = false;
        stop.Click += async delegate { await StopBackend(); };
        status.SetBounds(24, 544, 785, 28); status.Font = new Font("Malgun Gothic", 9F); status.ForeColor=Color.FromArgb(157,159,176);
        status.AutoEllipsis = true;
        status.Text = autoStart.Checked ? "osu! 실행 대기" : "대기 중"; Controls.Add(status);
        var credit = AddLabel("made by 0naire", 824, 544, 152, 28, 9F);
        credit.TextAlign = ContentAlignment.MiddleRight;
        credit.ForeColor=Color.FromArgb(224,156,181);
        tray.Icon=CreateTrayIcon();Icon=tray.Icon;tray.Text="Mania Helper · 대기 중";tray.Visible=true;
        var trayMenu=new ContextMenuStrip();
        trayMenu.Items.Add("설정 열기",null,delegate{ShowSettings();});
        trayMenu.Items.Add("도우미 중지",null,async delegate{if (backend != null) await StopBackend();});
        trayMenu.Items.Add(new ToolStripSeparator());trayMenu.Items.Add("종료",null,delegate{ExitHelper();});
        tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=delegate{ShowSettings();};
        LoadLayout();
        timer.Interval = 1500;
        timer.Tick += async delegate {
            if (reloadAutoStartRequest.WaitOne(0)) autoStart.Checked = AutoStart.IsEnabled;
            await UpdateAutomaticStart();
            await Poll();
        };
        timer.Start();
        Shown += async delegate { if (startDirect) await StartBackend(); else await UpdateAutomaticStart(); };
        previewTimer.Interval=250; previewTimer.Tick+=async delegate {if(showSettingsRequest.WaitOne(0))ShowSettings();if(Visible && WindowState != FormWindowState.Minimized){trace.Invalidate();await PollPreview();}};previewTimer.Start();
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!closing && autoStart.Checked && e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                Hide();
                return;
            }
            closing = true;
            timer.Stop();
            previewTimer.Stop();
            loading = true;
            preview.Checked = false;
            SaveLayout(false);
            KillOwned();
            tray.Visible = false;
            var icon = tray.Icon;
            tray.Dispose();
            if (icon != null) icon.Dispose();
            showSettingsRequest.Dispose();
            reloadAutoStartRequest.Dispose();
        };
    }
    public void CreateHandleForWatcher() { CreateHandle(); }
    void ExitHelper() { closing = true; Close(); }
    void ShowRankedMaps() {
        if (rankedMaps == null || rankedMaps.IsDisposed) {
            rankedMaps = new RankedMapsForm(() => OsuInstallation.Resolve(connectedGame, gamePath));
            rankedMaps.Show(this);
        } else {
            rankedMaps.WindowState = FormWindowState.Normal;
            rankedMaps.Activate();
        }
    }
    void ShowSettings(){Show();WindowState=FormWindowState.Normal;Activate();}
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static Icon CreateTrayIcon(){
        using(var bmp=new Bitmap(24,24))using(var g=Graphics.FromImage(bmp)){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
            using(var b=new SolidBrush(Color.FromArgb(234,165,192)))g.FillPolygon(b,new[]{new Point(5,3),new Point(19,3),new Point(12,10)});
            using(var p=new Pen(Color.White,2))foreach(int x in new[]{6,12,18})g.DrawLine(p,x,14,x,22);
            IntPtr h=bmp.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}
        }
    }
    Label AddLabel(string text, int left, int top, int w, int h, float size) {
        var label = new Label { Text = text, Font = new Font("Malgun Gothic", size), AutoSize = false };
        label.SetBounds(left, top, w, h); Controls.Add(label); return label;
    }
    void ConfigureNumber(NumericUpDown number, string title, int left, int top, decimal min, decimal max,int fieldWidth=145) {
        AddLabel(title, left, top, fieldWidth, 24, 10);
        number.SetBounds(left, top + 30, fieldWidth, 31); number.Minimum = min; number.Maximum = max;
        number.BackColor = Color.FromArgb(36, 37, 48); number.ForeColor = ForeColor;
        number.BorderStyle = BorderStyle.FixedSingle;
        number.ValueChanged += delegate { if (!loading) SaveLayout(backend != null); }; Controls.Add(number);
    }
    void SetButton(Button b, string text, int left, int top, int w, int h) {
        b.Text = text; b.SetBounds(left, top, w, h); b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Color.FromArgb(39, 41, 53); b.ForeColor = ForeColor; Controls.Add(b);
    }
    void LoadLayout() {
        loading = true;
        try {
            var obj = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(LayoutPath));
            SetNumber(x, obj, "x", 425); SetNumber(y, obj, "y", 164); SetNumber(width, obj, "width", 80); SetNumber(size,obj,"size",120);
            SetNumber(statsX,obj,"statsX",660);SetNumber(statsY,obj,"statsY",86);SetNumber(statsSize,obj,"statsSize",110);
            statsEnabled.Checked=!obj.ContainsKey("statsEnabled")||Convert.ToBoolean(obj["statsEnabled"]);
            graphEnabled.Checked=!obj.ContainsKey("graphEnabled")||Convert.ToBoolean(obj["graphEnabled"]);
            playerEnabled.Checked=!obj.ContainsKey("playerEnabled")||Convert.ToBoolean(obj["playerEnabled"]);
            if(obj.ContainsKey("comboPosition"))comboPosition=Convert.ToDouble(obj["comboPosition"]);
            if (obj.ContainsKey("skin") && !String.IsNullOrWhiteSpace(Convert.ToString(obj["skin"])))
                skin = Convert.ToString(obj["skin"]);
            if (obj.ContainsKey("gamePath")) gamePath = Convert.ToString(obj["gamePath"]);
        } catch { x.Value=425; y.Value=164; width.Value=80; size.Value=120; statsX.Value=660;statsY.Value=86;statsSize.Value=110; }
        loading = false; skinLabel.Text = "스킨: " + skin; SaveLayout(false);
        WritePlayer(new OsuPlayerProfile{status="Waiting for the logged-in player"});
    }
    static void SetNumber(NumericUpDown n, Dictionary<string, object> d, string key, decimal fallback) {
        decimal value = d.ContainsKey(key) ? Convert.ToDecimal(d[key]) : fallback;
        n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, value));
    }
    static void AtomicWrite(string path, string text) {
        string tmp = path + ".new";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
    void SaveLayout(bool enabled) {
        try {
            trace.SetPlacement((double)x.Value,(double)y.Value,(double)width.Value,(double)size.Value,comboPosition);
            statsPreview.SetPlacement((int)statsX.Value,(int)statsY.Value,(int)statsSize.Value,statsEnabled.Checked,graphEnabled.Checked,playerEnabled.Checked);
            AtomicWrite(LayoutPath, json.Serialize(new {
                x = (double)x.Value, y = (double)y.Value, width = (double)width.Value, size=(double)size.Value,comboPosition=comboPosition,
                statsX=(double)statsX.Value,statsY=(double)statsY.Value,statsSize=(double)statsSize.Value,statsEnabled=statsEnabled.Checked,
                graphEnabled=graphEnabled.Checked,playerEnabled=playerEnabled.Checked,
                skin = skin, gamePath = gamePath, preview = preview.Checked, enabled = enabled
            }));
        } catch (Exception e) { if (!closing) status.Text = "위치 저장 실패: " + e.Message; }
    }
    bool ChooseGame() {
        using (var dialog = new OpenFileDialog()) {
            dialog.Title = "osu!stable 위치 선택";
            dialog.Filter = "osu!stable (osu!.exe)|osu!.exe";
            if (OsuInstallation.ValidExecutable(gamePath) != null)
                dialog.InitialDirectory = Path.GetDirectoryName(gamePath);
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            string selected = OsuInstallation.ValidExecutable(dialog.FileName);
            if (selected == null) return false;
            gamePath = selected;
            SaveLayout(backend != null);
            return true;
        }
    }
    void FitToSkin() {
        try {
            string resolved = OsuInstallation.Resolve(connectedGame, gamePath);
            if (resolved != null) gamePath = resolved;
            else if (!ChooseGame()) return;
            string dir = Path.GetDirectoryName(gamePath);
            string cfg = OsuInstallation.ConfigPath(gamePath, Environment.UserName);
            string text = File.ReadAllText(cfg);
            Match name = Regex.Match(text, @"(?m)^Skin\s*=\s*(.+)$");
            if (!name.Success) throw new Exception("현재 스킨 이름을 찾을 수 없습니다.");
            skin = name.Groups[1].Value.Trim();
            string ini = File.ReadAllText(Path.Combine(dir, "Skins", skin, "skin.ini"));
            int pos = ini.IndexOf("Keys: 4", StringComparison.OrdinalIgnoreCase);
            if (pos < 0) pos = ini.IndexOf("Keys:4", StringComparison.OrdinalIgnoreCase);
            string section = pos < 0 ? ini : ini.Substring(pos);
            int next = section.IndexOf("[Mania]", 1, StringComparison.OrdinalIgnoreCase);
            if (next >= 0) section = section.Substring(0, next);
            double column = ReadValue(section, "ColumnStart", 285);
            double combo = ReadValue(section, "ComboPosition", 121);
            comboPosition=combo;
            var widths = Regex.Match(section, @"(?im)^ColumnWidth\s*:\s*([\d, .]+)");
            double total = 280;
            if (widths.Success) {
                string[] values = widths.Groups[1].Value.Split(','); total=0;
                foreach(string value in values) total += double.Parse(value.Trim(), CultureInfo.InvariantCulture);
                if(values.Length==1) total *= 4;
            }
            loading = true;
            x.Value = Clamp(x, (decimal)(column + total / 2));
            y.Value = Clamp(y, (decimal)(combo + 43));
            width.Value = Clamp(width, (decimal)(total * .2857));
            statsX.Value=Clamp(statsX,(decimal)(column+total+95));statsY.Value=Clamp(statsY,(decimal)Math.Max(0,combo-35));
            loading = false; skinLabel.Text = "스킨: " + skin; SaveLayout(backend != null);
            if (backend == null) status.Text = "스킨 적용 완료";
        } catch (Exception e) { loading=false; MessageBox.Show(this, e.Message, "스킨 확인"); }
    }
    static decimal Clamp(NumericUpDown n, decimal v) { return Math.Max(n.Minimum, Math.Min(n.Maximum,v)); }
    static double ReadValue(string text, string key, double fallback) {
        var m = Regex.Match(text, @"(?im)^" + key + @"\s*:\s*([\d.]+)");
        return m.Success ? double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture) : fallback;
    }
    void PrepareOverlaySize(string executable) {
        int w=Screen.PrimaryScreen.Bounds.Width, h=Screen.PrimaryScreen.Bounds.Height;
        try{
            string cfg = executable == null ? "" : File.ReadAllText(OsuInstallation.ConfigPath(executable, Environment.UserName));
            bool full=Regex.IsMatch(cfg,@"(?m)^Fullscreen\s*=\s*1\s*$");
            var wm=Regex.Match(cfg,@"(?m)^"+(full?"WidthFullscreen":"Width")+@"\s*=\s*(\d+)");
            var hm=Regex.Match(cfg,@"(?m)^"+(full?"HeightFullscreen":"Height")+@"\s*=\s*(\d+)");
            if(wm.Success && hm.Success){int rw=int.Parse(wm.Groups[1].Value),rh=int.Parse(hm.Groups[1].Value);if(rw>=640 && rh>=480){w=rw;h=rh;}}
        }catch{}
        var overlay=new { _enabled=true,_settings=false,id=1,folderName="ZeroTimingBar",url="http://127.0.0.1:24050/ZeroTimingBar/",width=w,height=h,top=0,left=0,scale=1,z_index=1 };
        var config=new {obs_profile="default",ingame_profile="default",profiles=new[]{new{id="default",name="default",overlays=new[]{overlay}}}};
        AtomicWrite(Path.Combine(Runtime,"settings","__ingame__.values.json"),json.Serialize(config));
        sizedFor = executable;
    }
    int RunningGameId() {
        bool lazerRunning;
        return OsuInstallation.RunningStableId(sessionId, out lazerRunning);
    }
    async Task UpdateAutomaticStart() {
        if (closing || starting || stopping || (!autoStart.Checked && !startedAutomatically)) return;
        bool lazerRunning;
        int gameId = OsuInstallation.RunningStableId(sessionId, out lazerRunning);
        if (startedAutomatically && automaticGameId != gameId) await StopBackend(true);
        if (gameId == 0) {
            if (!closing && backend == null && autoStart.Checked) {
                if (lazerRunning) status.Text = "osu!stable을 실행해 주세요 · lazer 감지됨";
                else if (handledGameId != 0 || status.Text.EndsWith("lazer 감지됨")) status.Text = "osu! 실행 대기";
            }
            handledGameId = 0;
            return;
        }
        if (closing || !autoStart.Checked || backend != null || handledGameId == gameId) return;
        handledGameId = gameId;
        await StartBackend(gameId);
    }
    async Task StartBackend(int gameId = 0, bool retry = false) {
        if (closing || backend != null || stopping || starting) return;
        starting=true; start.Enabled=false;
        if (!retry) connectionRetry.Reset();
        try {
            if (!File.Exists(Path.Combine(Runtime, "static", "ZeroTimingBar", "index.html")) ||
                !File.Exists(Path.Combine(Runtime, "tosu.env")) || !Directory.Exists(Path.Combine(Runtime, "settings")))
                throw new Exception("오버레이 파일이 없습니다. runtime 폴더를 확인해 주세요.");
            if(!File.Exists(Path.Combine(Runtime,"tosu.exe")) || !File.Exists(Path.Combine(Runtime,"game-overlay","tosu-ingame-overlay.exe")))
                throw new Exception("연결 구성 요소가 없습니다. 폴더의 Setup.cmd를 한 번 실행한 뒤 다시 시작해 주세요.");
            // A pre-existing local tosu belongs to the user; do not attach to or stop it.
            using (var probe = new TcpClient()) {
                var pending=probe.ConnectAsync("127.0.0.1",24050);
                if (await Task.WhenAny(pending,Task.Delay(300))==pending && probe.Connected)
                    throw new Exception("24050 포트를 다른 프로그램이 사용 중입니다. 기존 tosu를 종료한 후 다시 시작해 주세요.");
            }
            if(closing)return;
            if (gameId != 0 && RunningGameId() != gameId) {
                start.Enabled = true;
                status.Text = "osu!stable 실행 대기";
                return;
            }
            status.Text="연결 중…";
            string resolved = OsuInstallation.Resolve(null, gamePath);
            if (resolved != null) gamePath = resolved;
            PrepareOverlaySize(resolved);
            SaveLayout(true);
            job=new ChildJob();
            var info = new ProcessStartInfo(Path.Combine(Runtime,"tosu.exe")) {
                WorkingDirectory=Runtime,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            backend=new Process { StartInfo=info };
            backend.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { Log(e.Data); };
            backend.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { Log(e.Data); };
            backend.Start();
            job.Add(backend);
            startedAutomatically = gameId != 0;
            automaticGameId = gameId;
            backend.BeginOutputReadLine(); backend.BeginErrorReadLine();
            stop.Enabled=true;
            await Task.Delay(900);
        } catch (Exception e) {
            KillOwned(); SaveLayout(false); start.Enabled=true; stop.Enabled=false;
            if (!closing) {
                status.Text = e.Message;
                if (gameId == 0) MessageBox.Show(this, e.Message, "도우미 시작");
                else if (!Visible) tray.ShowBalloonTip(4000, "Mania Helper", e.Message, ToolTipIcon.Warning);
            }
        } finally {starting=false;}
    }
    readonly object logLock = new object();
    void Log(string line) {
        if (line==null) return;
        try { lock(logLock) File.AppendAllText(Path.Combine(root,"last-run.log"),line+Environment.NewLine); } catch { }
    }
    async Task StopBackend(bool gameClosed = false) {
        if(stopping) return;
        connectionRetry.Reset();
        if (!gameClosed && autoStart.Checked) handledGameId = RunningGameId();
        stopping=true; stop.Enabled=false; preview.Checked=false; SaveLayout(false);
        status.Text="중지 중…";
        await Task.Delay(950); KillOwned(); stopping=false;
        if(!closing){start.Enabled=true;status.Text=gameClosed && autoStart.Checked ? "osu! 실행 대기" : "중지됨";}
    }
    void KillOwned() {
        connectedGame = null;
        hasGameData = false;
        if(backend!=null){try{if(!backend.HasExited)backend.Kill();}catch{}backend.Dispose();backend=null;}
        if(job!=null){job.Dispose();job=null;}
        startedAutomatically = false;
        automaticGameId = 0;
        trace.UseDemo();
        statsPreview.UseDemo();
        if(!closing)tray.Text="Mania Helper · 중지됨";
    }
    async Task PollPreview(){
        if(previewPolling||closing||stopping||backend==null||!hasGameData)return;
        previewPolling=true;
        var connectedBackend = backend;
        try{
            var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:24050/json/v2/precise");req.Proxy=null;req.Timeout=800;
            using (var timeout = new CancellationTokenSource(800))
            using (timeout.Token.Register(req.Abort))
            using(var response=await req.GetResponseAsync())using(var reader=new StreamReader(response.GetResponseStream())){
                var d=json.Deserialize<Dictionary<string,object>>(await reader.ReadToEndAsync());
                if(closing || stopping || backend != connectedBackend)return;
                object value;
                if(d.TryGetValue("hitErrors",out value)){
                    var list=value as System.Collections.IList;
                    if(list!=null){var tail=new List<double>();for(int i=Math.Max(0,list.Count-12);i<list.Count;i++)tail.Add(Convert.ToDouble(list[i]));trace.SetData(tail.ToArray(),list.Count);statsPreview.SetTimings(list);}
                }
            }
        }catch{}finally{previewPolling=false;}
    }
    async Task WaitForGame() {
        connectedGame = null;
        if (hasGameData) {
            hasGameData = false;
            trace.UseDemo();
            statsPreview.UseDemo();
        }
        bool lazerRunning;
        int gameId = OsuInstallation.RunningStableId(sessionId, out lazerRunning);
        if (connectionRetry.ShouldRestart(gameId, DateTime.UtcNow)) {
            int automaticId = automaticGameId;
            stopping = true;
            status.Text = "osu! 다시 연결 중…";
            KillOwned();
            try { await Task.Delay(900); }
            finally { stopping = false; }
            if (!closing) await StartBackend(automaticId, true);
            return;
        }
        status.Text = gameId == 0 ? (lazerRunning ? "osu!stable을 실행해 주세요 · lazer 감지됨" : "osu!stable 실행 대기") :
            connectionRetry.HasRetried(gameId) ? "연결 지연 · osu!와 Helper 실행 권한 확인" : "osu!stable 연결 중…";
    }
    async Task Poll() {
        if(polling || closing || starting || stopping || backend==null) return;
        polling=true;
        var connectedBackend = backend;
        bool unavailable = false;
        try {
            if(connectedBackend.HasExited){KillOwned();SaveLayout(false);start.Enabled=true;stop.Enabled=false;status.Text="연결 종료됨";return;}
            var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:24050/json/v2");
            req.Timeout=1000;req.ReadWriteTimeout=1000;req.Proxy=null;
            using (var timeout = new CancellationTokenSource(1500))
            using (timeout.Token.Register(req.Abort))
            using(var response=await req.GetResponseAsync())
            using(var reader=new StreamReader(response.GetResponseStream())) {
                var d=json.Deserialize<Dictionary<string,object>>(await reader.ReadToEndAsync());
                if(closing || stopping || backend != connectedBackend)return;
                if (d == null || d.ContainsKey("error")) throw new InvalidDataException();
                var state = ReadObject(d, "state");
                if (state == null) throw new InvalidDataException();
                object foldersValue, folderValue;
                if (d.TryGetValue("folders", out foldersValue)) {
                    var folders = foldersValue as Dictionary<string, object>;
                    if (folders != null && folders.TryGetValue("game", out folderValue) && folderValue is string) {
                        connectedGame = OsuInstallation.ValidExecutable(Path.Combine((string)folderValue, "osu!.exe"));
                        if (connectedGame != null && (!String.Equals(sizedFor, connectedGame, StringComparison.OrdinalIgnoreCase) ||
                                !String.Equals(gamePath, connectedGame, StringComparison.OrdinalIgnoreCase))) {
                            gamePath = connectedGame;
                            try {
                                PrepareOverlaySize(gamePath);
                                SaveLayout(true);
                            } catch (IOException e) { Log(e.Message); }
                            catch (UnauthorizedAccessException e) { Log(e.Message); }
                        }
                    }
                }
                var currentSkin = ReadObject(ReadObject(d, "settings"), "skin");
                var play = ReadObject(d, "play");
                var profile = ReadObject(d, "profile");
                if (currentSkin != null && currentSkin.ContainsKey("name")) skinLabel.Text = "스킨: " + Convert.ToString(currentSkin["name"]);
                if (play != null) statsPreview.SetPlay(play);
                if (playerEnabled.Checked && profile != null) playerTask = UpdatePlayer(profile);
                hasGameData = true;
                connectionRetry.Reset();
                tray.Text="Mania Helper · 실행 중";
                object stateName;
                status.Text = state.TryGetValue("name", out stateName) && Convert.ToString(stateName) == "play" ? "플레이 중" : "연결됨";
            }
        } catch (WebException e) {
            if (e.Response != null) e.Response.Dispose();
            unavailable = true;
        } catch (InvalidDataException) {
            unavailable = true;
        } catch (Exception e) {
            Log(e.Message);
            unavailable = true;
        } finally {
            polling = false;
        }
        if (unavailable && !closing && !stopping && backend == connectedBackend) await WaitForGame();
    }
    static Dictionary<string, object> ReadObject(Dictionary<string, object> data, string key) {
        object value;
        return data != null && data.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
    }
    void WritePlayer(OsuPlayerProfile p){try{AtomicWrite(Path.Combine(Runtime,"static","ZeroTimingBar","player.json"),json.Serialize(p));statsPreview.SetPlayer(p);}catch{}}
    async Task UpdatePlayer(Dictionary<string,object> profile){
        object raw;int id=profile.TryGetValue("id",out raw)?Convert.ToInt32(raw):0;
        if(id!=currentProfileId){currentProfileId=id;lastProfileFetch=DateTime.MinValue;string country="";if(profile.TryGetValue("countryCode",out raw)){var c=raw as Dictionary<string,object>;if(c!=null&&c.TryGetValue("name",out raw))country=Convert.ToString(raw);}WritePlayer(new OsuPlayerProfile{id=id,name=profile.ContainsKey("name")?Convert.ToString(profile["name"]):"PLAYER",countryCode=country,status="Fetching official 4K country rank"});}
        if(profilePolling||id<=0||DateTime.UtcNow-lastProfileFetch<TimeSpan.FromMinutes(10))return;
        profilePolling=true;lastProfileFetch=DateTime.UtcNow;
        try{var p=await OsuProfileProvider.FetchAsync(id);if(!closing&&currentProfileId==id)WritePlayer(p);}catch{}finally{profilePolling=false;}
    }
}

// Only this app's own backend and its children enter the job. osu! is never assigned.
internal sealed class SoftButton : Button {
    bool over;
    public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
    protected override void OnMouseEnter(EventArgs e){over=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){over=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(Parent!=null)g.Clear(Parent.BackColor);
        Color fill=!Enabled?Color.FromArgb(28,29,38):over?ControlPaint.Light(BackColor,.12F):BackColor;
        using(var p=Rounded(new RectangleF(0,0,Width-1,Height-1),12))using(var b=new SolidBrush(fill))g.FillPath(b,p);
        TextRenderer.DrawText(g,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(90,92,110),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(Focused)using(var pen=new Pen(Color.FromArgb(105,ForeColor),1))using(var path=Rounded(new RectangleF(3,3,Width-7,Height-7),9))g.DrawPath(pen,path);
    }
    internal static GraphicsPath Rounded(RectangleF r,float radius){
        var p=new GraphicsPath();float d=radius*2;
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}
internal sealed class TracePreview : Control {
    double locationX=425,locationY=164,range=80,percent=120,comboY=121;
    double[] errors=new double[]{-32,-14,8,42};
    bool live;int lastCount=-1;DateTime lastHit=DateTime.UtcNow;
    public TracePreview(){DoubleBuffered=true;}
    public void SetPlacement(double x,double y,double width,double size,double combo){locationX=x;locationY=y;range=width;percent=size;comboY=combo;Invalidate();}
    public void SetData(double[] values,int count){live=true;if(count!=lastCount){errors=values;lastCount=count;lastHit=DateTime.UtcNow;}Invalidate();}
    public void UseDemo(){live=false;lastCount=-1;errors=new double[]{-32,-14,8,42};Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var p=SoftButton.Rounded(new RectangleF(0,0,Width-1,Height-1),16))using(var b=new SolidBrush(Color.FromArgb(28,29,39)))g.FillPath(b,p);
        using(var font=new Font("Malgun Gothic",9F))using(var brush=new SolidBrush(Color.FromArgb(151,154,173)))
            g.DrawString(live?"실시간":"미리보기",font,brush,17,13);
        float cx=(float)(Width/2+(locationX-425)*1.1), cy=(float)(64+(locationY-comboY)*1.1);
        var oldClip=g.Clip;g.SetClip(new Rectangle(12,37,Width-24,110));
        using(var font=new Font("Segoe UI",19F))using(var brush=new SolidBrush(Color.FromArgb(235,237,242)))g.DrawString("1234",font,brush,Width/2-32,37);
        float magnify=(float)(percent/100.0),half=9*magnify,triangle=5*magnify;
        using(var b=new SolidBrush(Color.FromArgb(237,181,201)))g.FillPolygon(b,new[]{new PointF(cx-triangle,cy-half-10*magnify),new PointF(cx+triangle,cy-half-10*magnify),new PointF(cx,cy-half-4*magnify)});
        using(var p=new Pen(Color.FromArgb(243,88,121),Math.Max(1,1.7F*magnify)))g.DrawLine(p,cx,cy-half,cx,cy+half);
        double age=(DateTime.UtcNow-lastHit).TotalMilliseconds;
        int alpha=live?(int)(230*Math.Max(0,1-age/1200)):230;
        if(alpha>0)using(var p=new Pen(Color.FromArgb(alpha,235,237,242),Math.Max(1,1.7F*magnify)))foreach(double error in errors){float dx=(float)(error/121.0*range*.55*magnify);g.DrawLine(p,cx+dx,cy-half,cx+dx,cy+half);}
        g.Clip=oldClip;oldClip.Dispose();
    }
}
internal sealed class ChildJob : IDisposable {
    IntPtr handle;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr a,string n);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr h,int c,IntPtr p,uint n);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr h,IntPtr p);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long a,b;public uint flags;public UIntPtr min,max;public uint active;public UIntPtr affinity;public uint priority,scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong a,b,c,d,e,f; }
    [StructLayout(LayoutKind.Sequential)] struct Extended { public Basic basic;public Io io;public UIntPtr process,job,peakProcess,peakJob; }
    public ChildJob() {
        handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
        var info=new Extended(); info.basic.flags=0x2000;
        int size=Marshal.SizeOf(info);IntPtr p=Marshal.AllocHGlobal(size);
        try{Marshal.StructureToPtr(info,p,false);if(!SetInformationJobObject(handle,9,p,(uint)size))throw new System.ComponentModel.Win32Exception();}
        catch{Dispose();throw;}finally{Marshal.FreeHGlobal(p);}
    }
    public void Add(Process p){if(!AssignProcessToJobObject(handle,p.Handle))throw new System.ComponentModel.Win32Exception();}
    public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
}
