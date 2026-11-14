using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class RankedMapsForm : Form {
    static readonly Color Background = Color.FromArgb(18, 19, 25);
    static readonly Color Surface = Color.FromArgb(29, 30, 41);
    static readonly Color Muted = Color.FromArgb(167, 169, 190);
    static readonly Color Pink = Color.FromArgb(235, 162, 195);
    const int FarmPageSize = 50;
    readonly Func<string> findGame;
    readonly TextBox search = new TextBox(), ppRange = new TextBox(), starRange = new TextBox();
    readonly ComboBox keys = new ComboBox(), sort = new ComboBox(), mods = new ComboBox();
    readonly DataGridView grid = new DataGridView();
    readonly Button latest = new SoftButton(), farm = new SoftButton(), refresh = new SoftButton();
    readonly Button previous = new SoftButton(), next = new SoftButton(), download = new SoftButton(), open = new SoftButton();
    readonly Label status = new Label(), ppLabel = new Label(), pageLabel = new Label();
    readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer();
    readonly Dictionary<int, RankedMapPage> latestPages = new Dictionary<int, RankedMapPage>();
    readonly Dictionary<int, double> recentPp = new Dictionary<int, double>();
    readonly Dictionary<int, string> downloads = new Dictionary<int, string>();
    List<RankedMap> farmMaps;
    List<RankedMap> visible = new List<RankedMap>();
    CancellationTokenSource catalogRequest, downloadRequest;
    bool useFarm, closing, updatingControls;
    int page;
    string latestQuery;

    public RankedMapsForm(Func<string> findGame) {
        this.findGame = findGame;
        Text = "Mania Helper · 매니아 랭크맵";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1056, 655);
        MinimumSize = new Size(1055, 480);
        Font = new Font("Malgun Gothic", 9F);
        BackColor = Background;
        ForeColor = Color.FromArgb(242, 242, 248);
        var title = new Label { Text = "매니아 랭크맵", Font = new Font(Font.FontFamily, 19F, FontStyle.Bold), ForeColor = ForeColor };
        title.SetBounds(22, 16, 270, 41);
        Controls.Add(title);
        ppLabel.TextAlign = ContentAlignment.MiddleRight;
        ppLabel.ForeColor = Muted;
        ppLabel.SetBounds(720, 23, 312, 26);
        ppLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(ppLabel);
        PlaceButton(latest, "최신 랭크", 24, 67, 108, 34, () => SwitchCatalog(false));
        PlaceButton(farm, "PP 파밍", 140, 67, 105, 34, () => SwitchCatalog(true));
        PlaceButton(refresh, "새로고침", 925, 67, 107, 34, () => Reload());
        refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        PlaceLabel("곡 / 아티스트 / 매퍼 / ID", 24, 113, 285);
        PlaceLabel("키 수", 327, 113, 88);
        PlaceLabel("모드", 427, 113, 88);
        PlaceLabel("PP 범위", 527, 113, 99);
        PlaceLabel("별점 범위", 638, 113, 99);
        PlaceLabel("정렬", 749, 113, 157);
        PlaceText(search, 24, 135, 288, 100);
        PlaceText(ppRange, 527, 135, 100, 17);
        PlaceText(starRange, 638, 135, 100, 17);
        var tips = new ToolTip();
        tips.SetToolTip(ppRange, "예: 100-250, 100-, -250");
        tips.SetToolTip(starRange, "예: 3-5, 3-, -5");
        Disposed += delegate { tips.Dispose(); debounce.Dispose(); };
        PlaceCombo(keys, 327, 134, 88, new[] { "전체" }.Concat(Enumerable.Range(1, 18).Select(n => n + "K")).ToArray());
        PlaceCombo(mods, 427, 134, 88, new[] { "전체", "NM", "DT", "HT", "EZ", "EZDT", "EZHT" });
        PlaceCombo(sort, 749, 134, 157, new[] { "최신 랭크순", "PP 높은순", "PP 낮은순", "별점 높은순",
            "별점 낮은순", "BPM 높은순", "짧은 맵순" });
        sort.SelectedIndex = 0;
        ConfigureGrid();
        grid.SetBounds(24, 178, 1008, 409);
        grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(grid);

        status.ForeColor = Muted;
        status.AutoEllipsis = true;
        status.SetBounds(24, 604, 329, 32);
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(status);
        PlaceButton(previous, "이전", 371, 604, 70, 32, () => ChangePage(-1));
        PlaceButton(next, "다음", 568, 604, 70, 32, () => ChangePage(1));
        pageLabel.SetBounds(445, 604, 119, 32);
        pageLabel.ForeColor = Muted;
        pageLabel.TextAlign = ContentAlignment.MiddleCenter;
        pageLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(pageLabel);
        PlaceButton(open, "osu! 페이지", 706, 600, 130, 38, OpenSelected);
        PlaceButton(download, "다운로드 및 가져오기", 848, 600, 184, 38, async () => await DownloadSelected());
        previous.Anchor = next.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        open.Anchor = download.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        download.BackColor = Pink;
        download.ForeColor = Background;

        debounce.Interval = 325;
        debounce.Tick += async delegate { debounce.Stop(); await LoadPage(); };
        search.TextChanged += delegate { UpdateFilters(); };
        search.KeyDown += async delegate(object sender, KeyEventArgs e) {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            debounce.Stop();
            await LoadPage();
        };
        ppRange.TextChanged += delegate { UpdateFilters(); };
        starRange.TextChanged += delegate { UpdateFilters(); };
        keys.SelectedIndexChanged += delegate { UpdateFilters(); };
        mods.SelectedIndexChanged += delegate { UpdateFilters(); };
        sort.SelectedIndexChanged += delegate { UpdateFilters(); };
        grid.SelectionChanged += delegate { UpdateDownloadButtons(); };
        grid.CellDoubleClick += async delegate(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex >= 0) await DownloadSelected();
        };
        FormClosed += delegate {
            closing = true;
            debounce.Stop();
            if (catalogRequest != null) catalogRequest.Cancel();
            if (downloadRequest != null) downloadRequest.Cancel();
        };
        SetCatalogStyle();
        ShowRows(new List<RankedMap>(), false);
        Shown += async delegate { await LoadPage(); };
    }

    void PlaceButton(Button button, string text, int x, int y, int width, int height, Action click) {
        button.Text = text;
        button.BackColor = Surface;
        button.ForeColor = ForeColor;
        button.FlatStyle = FlatStyle.Flat;
        button.SetBounds(x, y, width, height);
        button.Click += delegate { click(); };
        Controls.Add(button);
    }
    void PlaceLabel(string text, int x, int y, int width) {
        var label = new Label { Text = text, ForeColor = Muted };
        label.SetBounds(x, y, width, 21);
        Controls.Add(label);
    }
    void PlaceText(TextBox box, int x, int y, int width, int maxLength) {
        box.MaxLength = maxLength;
        box.BackColor = Surface;
        box.ForeColor = ForeColor;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.SetBounds(x, y, width, 29);
        Controls.Add(box);
    }
    void PlaceCombo(ComboBox box, int x, int y, int width, string[] items) {
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Surface;
        box.ForeColor = ForeColor;
        box.SetBounds(x, y, width, 29);
        box.Items.AddRange(items);
        box.SelectedIndex = 0;
        Controls.Add(box);
    }
    void ConfigureGrid() {
        grid.VirtualMode = true;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Background;
        grid.GridColor = Color.FromArgb(39, 40, 53);
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 35;
        grid.RowTemplate.Height = 33;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
        grid.DefaultCellStyle.BackColor = Background;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(23, 24, 32);
        grid.DefaultCellStyle.ForeColor = ForeColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 43, 67);
        grid.DefaultCellStyle.SelectionForeColor = ForeColor;
        AddColumn("곡 · 아티스트", 240, DataGridViewContentAlignment.MiddleLeft, true);
        AddColumn("난이도", 147, DataGridViewContentAlignment.MiddleLeft);
        AddColumn("키", 43);
        AddColumn("모드", 51);
        AddColumn("PP", 62);
        AddColumn("★", 55);
        AddColumn("BPM", 53);
        AddColumn("길이", 53);
        AddColumn("OD", 45);
        AddColumn("HP", 45);
        AddColumn("랭크 날짜", 88);
        grid.Columns[4].DefaultCellStyle.ForeColor = Pink;
        grid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
            if (e.RowIndex >= 0 && e.RowIndex < visible.Count) e.Value = Cell(visible[e.RowIndex], e.ColumnIndex);
        };
        grid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e) {
            if (e.RowIndex < 0 || e.RowIndex >= visible.Count || e.ColumnIndex > 1) return;
            var map = visible[e.RowIndex];
            e.ToolTipText = map.Artist + " — " + map.Title + " [" + map.Difficulty + "]" +
                (String.IsNullOrEmpty(map.Mapper) ? "" : "\r\n" + map.Mapper);
        };
        grid.ColumnHeaderMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e) {
            switch (e.ColumnIndex) {
                case 4: sort.SelectedIndex = sort.SelectedIndex == 1 ? 2 : 1; break;
                case 5: sort.SelectedIndex = sort.SelectedIndex == 3 ? 4 : 3; break;
                case 6: sort.SelectedIndex = 5; break;
                case 7: sort.SelectedIndex = 6; break;
                case 10: sort.SelectedIndex = 0; break;
            }
        };
    }
    void AddColumn(string label, int width, DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleRight, bool fill = false) {
        var column = new DataGridViewTextBoxColumn {
            HeaderText = label, Width = width, SortMode = DataGridViewColumnSortMode.Programmatic,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
        };
        column.DefaultCellStyle.Alignment = alignment;
        column.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.Columns.Add(column);
    }
    static string Cell(RankedMap map, int column) {
        switch (column) {
            case 0: return map.Artist + " — " + map.Title;
            case 1: return map.Difficulty;
            case 2: return map.Keys > 0 ? map.Keys + "K" : "—";
            case 3: return map.Mods;
            case 4: return Format(map.Pp, "0.#");
            case 5: return Format(map.Stars, "0.00");
            case 6: return Format(map.Bpm, "0.#");
            case 7: return map.Seconds > 0 ? (map.Seconds / 60) + ":" + (map.Seconds % 60).ToString("00") : "—";
            case 8: return Format(map.Od, "0.#");
            case 9: return Format(map.Hp, "0.#");
            default: return map.RankedAt.HasValue ? map.RankedAt.Value.ToLocalTime().ToString("yyyy-MM-dd") : "—";
        }
    }
    static string Format(double? value, string pattern) {
        return value.HasValue ? value.Value.ToString(pattern, CultureInfo.InvariantCulture) : "—";
    }
    void SetCatalogStyle() {
        latest.BackColor = useFarm ? Surface : Pink;
        latest.ForeColor = useFarm ? ForeColor : Background;
        farm.BackColor = useFarm ? Pink : Surface;
        farm.ForeColor = useFarm ? Background : ForeColor;
        mods.Enabled = useFarm;
        ppLabel.Text = useFarm ? "약 99% 기준 PP · osu-pps" : "NM · 100% 기준 PP · hinai";
    }
    void SwitchCatalog(bool farmView) {
        if (useFarm == farmView) return;
        useFarm = farmView;
        updatingControls = true;
        sort.SelectedIndex = useFarm ? 1 : 0;
        updatingControls = false;
        SetCatalogStyle();
        UpdateFilters();
    }
    void Reload() {
        if (useFarm) farmMaps = null;
        else { latestPages.Clear(); recentPp.Clear(); }
        UpdateFilters();
    }
    void UpdateFilters() {
        if (updatingControls || closing) return;
        page = 0;
        if (catalogRequest != null) catalogRequest.Cancel();
        previous.Enabled = next.Enabled = false;
        debounce.Stop();
        debounce.Start();
    }
    async void ChangePage(int delta) {
        if (page + delta < 0) return;
        int prior = page;
        page += delta;
        var operation = LoadPage();
        var request = catalogRequest;
        if (!await operation && !closing && request != null && !request.IsCancellationRequested &&
                catalogRequest == null && page == prior + delta) {
            page = prior;
            pageLabel.Text = (page + 1) + " 페이지";
            previous.Enabled = page > 0;
            next.Enabled = true;
        }
    }
    async Task<bool> LoadPage() {
        if (closing) return false;
        if (catalogRequest != null) catalogRequest.Cancel();
        var request = new CancellationTokenSource();
        catalogRequest = request;
        int displayPage = page;
        bool farmView = useFarm;
        string query = search.Text.Trim();
        string mod = mods.SelectedItem as string;
        int keyCount = keys.SelectedIndex;
        int order = sort.SelectedIndex;
        double? minPp, maxPp, minStars, maxStars;
        try {
            if (!ReadRange(ppRange.Text, out minPp, out maxPp) || !ReadRange(starRange.Text, out minStars, out maxStars)) {
                ShowRows(new List<RankedMap>(), false);
                SetStatus("범위 입력 예: 100-250 또는 3.5-5");
                return true;
            }
            previous.Enabled = next.Enabled = false;
            SetStatus("랭크맵 불러오는 중…");
            List<RankedMap> maps;
            bool more, missingPp = false;
            if (farmView) {
                if (farmMaps == null) {
                    var catalog = await RankedMaps.GetFarmMaps(request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    farmMaps = catalog;
                }
                var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var matches = farmMaps.Where(map => (mod == "전체" || map.Mods == mod) &&
                    words.All(word => map.SearchText.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0));
                var results = Sort(Filter(matches, keyCount, minPp, maxPp, minStars, maxStars), order)
                    .Skip(displayPage * FarmPageSize).Take(FarmPageSize + 1).ToList();
                more = results.Count > FarmPageSize;
                maps = results.Take(FarmPageSize).ToList();
            } else {
                if (latestQuery != query) { latestPages.Clear(); latestQuery = query; }
                RankedMapPage entry;
                if (!latestPages.TryGetValue(displayPage, out entry)) {
                    entry = await RankedMaps.GetLatestSets(query, displayPage * RankedMaps.SetsPerPage, request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    latestPages[displayPage] = entry;
                }
                more = entry.SetCount >= RankedMaps.SetsPerPage;
                var candidates = Filter(entry.Maps, keyCount, null, null, minStars, maxStars).ToList();
                var pending = candidates.Where(map => !recentPp.ContainsKey(map.Id)).Select(map => map.Id).ToArray();
                if (pending.Length > 0) {
                    if (!minPp.HasValue && !maxPp.HasValue) ShowRows(Sort(candidates, order).ToList(), more);
                    SetStatus("PP 불러오는 중…");
                    try {
                        var pp = await RankedMaps.GetLatestPp(pending, request.Token);
                        request.Token.ThrowIfCancellationRequested();
                        foreach (var value in pp) recentPp[value.Key] = value.Value;
                    } catch (Exception error) {
                        request.Token.ThrowIfCancellationRequested();
                        if (!(error is HttpRequestException || error is InvalidDataException || error is TaskCanceledException)) throw;
                    }
                }
                foreach (var map in candidates) {
                    double value;
                    map.Pp = recentPp.TryGetValue(map.Id, out value) ? (double?)value : null;
                }
                missingPp = candidates.Any(map => !map.Pp.HasValue);
                maps = Sort(Filter(candidates, 0, minPp, maxPp, null, null), order).ToList();
            }
            request.Token.ThrowIfCancellationRequested();
            if (closing) return false;
            ShowRows(maps, more);
            SetStatus(maps.Count == 0 ? (more ? "이 페이지에 조건에 맞는 맵이 없어요" : "조건에 맞는 맵이 없어요") :
                maps.Count + "개 난이도" + (missingPp ? " · 일부 PP 미제공" : "") +
                (!farmView && order != 0 ? " · 현재 페이지 정렬" : ""));
            return true;
        } catch (Exception error) {
            if (!closing && !request.IsCancellationRequested) {
                Debug.WriteLine(error);
                SetStatus("불러오기 실패 · 새로고침");
            }
            return false;
        } finally {
            if (catalogRequest == request) catalogRequest = null;
            request.Dispose();
        }
    }

    internal static bool ReadRange(string input, out double? low, out double? high) {
        low = high = null;
        string text = input.Trim();
        if (text.Length == 0) return true;
        var match = Regex.Match(text, @"^(?<low>\d+(?:\.\d+)?)?\s*(?<separator>[-~])?\s*(?<high>\d+(?:\.\d+)?)?$");
        if (!match.Success || (!match.Groups["low"].Success && !match.Groups["high"].Success) ||
                (match.Groups["high"].Success && !match.Groups["separator"].Success)) return false;
        if (match.Groups["low"].Success) low = Double.Parse(match.Groups["low"].Value, CultureInfo.InvariantCulture);
        if (match.Groups["high"].Success) high = Double.Parse(match.Groups["high"].Value, CultureInfo.InvariantCulture);
        if (!match.Groups["separator"].Success) high = low;
        return !low.HasValue || !high.HasValue || low <= high;
    }
    static IEnumerable<RankedMap> Filter(IEnumerable<RankedMap> maps, int keyCount, double? minPp, double? maxPp, double? minStars, double? maxStars) {
        return maps.Where(map => (keyCount == 0 || map.Keys == keyCount) &&
            (!minPp.HasValue || map.Pp >= minPp) && (!maxPp.HasValue || map.Pp <= maxPp) &&
            (!minStars.HasValue || map.Stars >= minStars) && (!maxStars.HasValue || map.Stars <= maxStars));
    }
    static IEnumerable<RankedMap> Sort(IEnumerable<RankedMap> maps, int order) {
        switch (order) {
            case 1: return maps.OrderByDescending(map => map.Pp);
            case 2: return maps.OrderBy(map => map.Pp ?? Double.MaxValue);
            case 3: return maps.OrderByDescending(map => map.Stars);
            case 4: return maps.OrderBy(map => map.Stars ?? Double.MaxValue);
            case 5: return maps.OrderByDescending(map => map.Bpm);
            case 6: return maps.OrderBy(map => map.Seconds > 0 ? map.Seconds : Int32.MaxValue);
            default: return maps.OrderByDescending(map => map.RankedAt);
        }
    }
    void ShowRows(List<RankedMap> rows, bool more) {
        var selected = SelectedMap();
        grid.RowCount = 0;
        visible = rows;
        grid.RowCount = rows.Count;
        if (rows.Count > 0 && selected != null) {
            int position = rows.FindIndex(map => map.Id == selected.Id && map.Mods == selected.Mods);
            if (position >= 0) grid.CurrentCell = grid.Rows[position].Cells[0];
        }
        pageLabel.Text = (page + 1) + " 페이지";
        previous.Enabled = page > 0;
        next.Enabled = more;
        UpdateDownloadButtons();
    }
    RankedMap SelectedMap() {
        int index = grid.CurrentCell == null ? -1 : grid.CurrentCell.RowIndex;
        return index >= 0 && index < visible.Count ? visible[index] : null;
    }
    void SetStatus(string text) {
        if (!closing && downloadRequest == null) status.Text = text;
    }
    void UpdateDownloadButtons() {
        var map = SelectedMap();
        open.Enabled = map != null;
        download.Enabled = map != null && downloadRequest == null;
        download.Text = map != null && downloads.ContainsKey(map.SetId) ? "osu!에서 열기" : "다운로드 및 가져오기";
    }
    void OpenSelected() {
        var map = SelectedMap();
        if (map == null) return;
        try { Process.Start("https://osu.ppy.sh/beatmapsets/" + map.SetId + "#mania/" + map.Id); }
        catch (System.ComponentModel.Win32Exception) { SetStatus("브라우저를 열 수 없어요"); }
    }
    async Task DownloadSelected() {
        var map = SelectedMap();
        if (map == null || downloadRequest != null || closing) return;
        var request = new CancellationTokenSource();
        downloadRequest = request;
        UpdateDownloadButtons();
        string path = null;
        try {
            if (!downloads.TryGetValue(map.SetId, out path) || !File.Exists(path)) {
                status.Text = "다운로드 중…";
                var progress = new Progress<int?>(percentage => {
                    if (!closing && downloadRequest == request && percentage.HasValue) status.Text = "다운로드 " + percentage + "%";
                });
                path = await RankedMaps.Download(map.SetId, progress, request.Token);
                request.Token.ThrowIfCancellationRequested();
                downloads[map.SetId] = path;
            }
            string game = findGame();
            if (game != null) {
                Process.Start(new ProcessStartInfo(game, "\"" + path + "\"") { WorkingDirectory = Path.GetDirectoryName(game), UseShellExecute = true });
            } else {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            status.Text = "osu!에 가져오기 요청 완료";
        } catch (Exception error) {
            Debug.WriteLine(error);
            if (!closing && !request.IsCancellationRequested) {
                if (path != null && File.Exists(path)) {
                    status.Text = "다운로드 완료 · osu!에서 열기 실패";
                    if (MessageBox.Show(this, "osu!에서 열지 못했어요. 다운로드한 파일을 표시할까요?", "매니아 랭크맵",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) {
                        try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
                        catch (System.ComponentModel.Win32Exception) { MessageBox.Show(this, path); }
                    }
                } else status.Text = "다운로드 실패 · osu! 페이지에서 시도";
            }
        } finally {
            downloadRequest = null;
            request.Dispose();
            if (!closing) UpdateDownloadButtons();
        }
    }
}
