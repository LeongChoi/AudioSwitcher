using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace AudioSwitcherModern
{
    public sealed class Preferences
    {
        public List<string> Favourites = new List<string>();
        public bool Communications = true;
        public bool Pinned;
        static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioSwitcherModern"); } }
        public static Preferences Load()
        {
            try { using (var stream = File.OpenRead(Path.Combine(Folder, "settings.xml"))) return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream); }
            catch { return new Preferences(); }
        }
        public void Save()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "settings.xml"), temp = path + ".tmp";
            using (var stream = File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public static void Log(Exception ex)
        {
            try { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "error.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine); }
            catch { }
        }
    }
    internal static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string value);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        internal static readonly uint ShowMessage = RegisterWindowMessage("AudioSwitcherModern.Show.1");
        [STAThread] static void Main(string[] args)
        {
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\AudioSwitcherModern.Oscar.1", out first)) {
                if (!first) { PostMessage(new IntPtr(0xffff), ShowMessage, IntPtr.Zero, IntPtr.Zero); return; }
                SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Preferences.Log(e.Exception); MessageBox.Show(e.Exception.Message, "Audio Switcher Modern"); };
                using (MainForm form = new MainForm(args.Any(a => a == "--tray"))) Application.Run(form);
            }
        }
    }
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(17, 20, 27);
        public static readonly Color Surface = Color.FromArgb(27, 32, 42);
        public static readonly Color Border = Color.FromArgb(49, 58, 73);
        public static readonly Color Text = Color.FromArgb(236, 241, 249);
        public static readonly Color Dim = Color.FromArgb(149, 163, 185);
        public static readonly Color Accent = Color.FromArgb(114, 170, 255);
        public static readonly Color Green = Color.FromArgb(108, 219, 179);
        public static Font Font(float size, FontStyle style) { return new Font("Segoe UI", size, style); }
        public static Label Label(string text, float size, Color color)
        { return new Label { Text = text, AutoSize = false, Font = Font(size, FontStyle.Regular), ForeColor = color, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }; }
        public static Button Button(string text)
        {
            Button button = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Text, Font = Font(9, FontStyle.Regular), Cursor = Cursors.Hand, Height = 30, TabStop = true };
            button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Border;
            return button;
        }
        public static GraphicsPath Round(RectangleF bounds, float radius)
        {
            float d = radius * 2; GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
    internal sealed class VolumeSlider : Control
    {
        int value;
        public bool Dragging { get { return Capture; } }
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public event EventHandler ValueChanged;
        public VolumeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            TabStop = true; Cursor = Cursors.Hand; Height = 32; BackColor = Theme.Surface;
            AccessibleName = "Volume"; AccessibleRole = AccessibleRole.Slider;
        }
        void Change(int amount) { int before = value; Value = amount; if (before != value && ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        void FromMouse(int x) { Change((int)Math.Round((x - 9) * 100.0 / Math.Max(1, Width - 18))); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); Capture = true; FromMouse(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) FromMouse(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Left) Capture = false; }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Change(value + Math.Sign(e.Delta) * ((ModifierKeys & Keys.Shift) != 0 ? 1 : 2)); }
        protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || base.IsInputKey(key); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Home) Change(0); else if (e.KeyCode == Keys.End) Change(100);
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Change(value - 1);
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Change(value + 1);
            else return;
            e.Handled = true;
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float y = Height / 2f, x = 9 + (Width - 18) * value / 100f;
            using (Pen track = new Pen(Theme.Border, 5)) { track.StartCap = track.EndCap = LineCap.Round; g.DrawLine(track, 9, y, Width - 9, y); }
            using (Pen fill = new Pen(Theme.Accent, 5)) { fill.StartCap = fill.EndCap = LineCap.Round; g.DrawLine(fill, 9, y, x, y); }
            using (Brush brush = new SolidBrush(Theme.Text)) g.FillEllipse(brush, x - 6, y - 6, 12, 12);
            if (Focused) using (Pen focus = new Pen(Theme.Accent)) g.DrawEllipse(focus, x - 9, y - 9, 18, 18);
        }
    }
    internal sealed class VolumeRow : Panel
    {
        readonly Label title;
        readonly VolumeSlider slider;
        readonly TextBox number;
        readonly Button mute;
        bool syncing, muted;
        DateTime localEdit = DateTime.MinValue;
        public event Action<int?, bool?> Changed;
        public VolumeRow(string name)
        {
            Height = 70; BackColor = Theme.Surface; Margin = new Padding(0, 6, 0, 9); Padding = new Padding(12, 4, 12, 4);
            title = Theme.Label(name, 9.5f, Theme.Text); title.Dock = DockStyle.Top; title.Height = 26;
            TableLayoutPanel controls = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3, BackColor = Theme.Surface, Padding = Padding.Empty, Margin = Padding.Empty };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            slider = new VolumeSlider { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), AccessibleName = name + " volume" };
            number = new TextBox { Text = "0%", Dock = DockStyle.Fill, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, Font = Theme.Font(10, FontStyle.Regular), TextAlign = HorizontalAlignment.Center, MaxLength = 4, Margin = new Padding(0, 5, 0, 0), AccessibleName = name + " volume percent (press Enter to apply)" };
            mute = Theme.Button("Mute"); mute.Dock = DockStyle.Fill; mute.Margin = Padding.Empty; mute.AccessibleName = name + " mute";
            controls.Controls.Add(slider, 0, 0); controls.Controls.Add(number, 1, 0); controls.Controls.Add(mute, 2, 0);
            Controls.Add(controls); Controls.Add(title);
            slider.ValueChanged += delegate { if (!syncing) { number.Text = slider.Value + "%"; Edit(slider.Value, null); } };
            number.Leave += delegate { CommitNumber(); };
            number.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { CommitNumber(); slider.Focus(); e.SuppressKeyPress = true; } };
            number.Enter += delegate { number.SelectAll(); };
            number.KeyPress += delegate(object sender, KeyPressEventArgs e) { if (!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar) && e.KeyChar != '%') e.Handled = true; };
            mute.Click += delegate { muted = !muted; PaintMute(); Edit(null, muted); };
        }
        void CommitNumber()
        {
            int parsed;
            if (Int32.TryParse(number.Text.Trim().TrimEnd('%'), out parsed)) { slider.Value = parsed; number.Text = slider.Value + "%"; Edit(slider.Value, null); }
            else number.Text = slider.Value + "%";
        }
        void Edit(int? level, bool? muteValue) { localEdit = DateTime.UtcNow; if (Changed != null) Changed(level, muteValue); }
        void PaintMute() { mute.Text = muted ? "Unmute" : "Mute"; mute.ForeColor = muted ? Theme.Green : Theme.Dim; }
        public void Sync(int volume, bool isMuted)
        {
            // Ignore old polling results briefly after an edit and never move a dragged thumb.
            if (slider.Dragging || number.ContainsFocus || (DateTime.UtcNow - localEdit).TotalMilliseconds < 1200) return;
            syncing = true; slider.Value = volume; number.Text = slider.Value + "%"; muted = isMuted; PaintMute(); syncing = false;
        }
    }
    internal sealed class DeviceButton : Button
    {
        internal DeviceInfo Device;
        public bool Favourite;
        bool starClick;
        public event Action FavouriteClicked;
        public DeviceButton(DeviceInfo device)
        {
            Device = device; Height = 48; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Theme.Surface;
            ForeColor = Theme.Text; Font = Theme.Font(9.5f, FontStyle.Regular); Cursor = Cursors.Hand; AccessibleName = device.Name;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseDown(MouseEventArgs e) { starClick = e.Button == MouseButtons.Left && e.X >= Width - 44; base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (starClick && e.X >= Width - 44 && e.Button == MouseButtons.Left) { if (FavouriteClicked != null) FavouriteClicked(); } base.OnMouseUp(e); starClick = false; }
        protected override void OnClick(EventArgs e) { if (!starClick) base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 4), 10)) {
                using (Brush b = new SolidBrush(Device.IsDefault ? Color.FromArgb(31, 46, 66) : Theme.Surface)) g.FillPath(b, path);
                if (Device.IsDefault || Focused) using (Pen p = new Pen(Theme.Accent)) g.DrawPath(p, path);
            }
            using (Pen p = new Pen(Device.IsDefault ? Theme.Accent : Theme.Dim, 1.5f)) g.DrawEllipse(p, 16, Height / 2 - 6, 12, 12);
            if (Device.IsDefault) using (Brush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 19, Height / 2 - 3, 6, 6);
            TextRenderer.DrawText(g, Device.Name, Font, new Rectangle(40, 3, Math.Max(1, Width - 86), Height - 7), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            using (Font f = Theme.Font(16, FontStyle.Regular)) TextRenderer.DrawText(g, Favourite ? "★" : "☆", f, new Rectangle(Width - 42, 0, 36, Height - 3), Favourite ? Theme.Accent : Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    internal sealed class WorkItem { public string Key; public Action Action; }
    internal sealed class AudioWorker : IDisposable
    {
        readonly object gate = new object();
        readonly LinkedList<WorkItem> queue = new LinkedList<WorkItem>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread thread;
        bool stopping;
        public AudioWorker()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "Windows audio" }; thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        }
        public void Post(string key, Action action)
        {
            lock (gate) {
                if (stopping) return;
                // Coalesce slider motion rather than replaying a backlog of old levels.
                if (key != null) {
                    LinkedListNode<WorkItem> node = queue.First;
                    while (node != null) { LinkedListNode<WorkItem> next = node.Next; if (node.Value.Key == key) queue.Remove(node); node = next; }
                }
                queue.AddLast(new WorkItem { Key = key, Action = action }); wake.Set();
            }
        }
        void Run()
        {
            while (true) {
                WorkItem work = null;
                lock (gate) { if (stopping) return; if (queue.Count > 0) { work = queue.First.Value; queue.RemoveFirst(); } }
                if (work == null) { wake.WaitOne(); continue; }
                try { work.Action(); } catch (Exception ex) { Preferences.Log(ex); }
            }
        }
        public void Dispose() { lock (gate) { stopping = true; queue.Clear(); wake.Set(); } if (thread.Join(1000)) wake.Dispose(); }
    }
    internal sealed class MainForm : Form
    {
        readonly Preferences prefs = Preferences.Load();
        readonly AudioWorker worker = new AudioWorker();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip settingsMenu;
        readonly Icon appIcon;
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        readonly FlowLayoutPanel outputs, sessions;
        readonly Panel body;
        readonly TableLayoutPanel contentStack;
        readonly Label status;
        readonly ComboBox inputs;
        readonly VolumeRow master, microphone;
        readonly Button pin, mixer;
        readonly List<DeviceButton> deviceButtons = new List<DeviceButton>();
        readonly Dictionary<string, VolumeRow> sessionRows = new Dictionary<string, VolumeRow>();
        readonly bool trayStart;
        string deviceSignature = "", sessionSignature = "", masterId, micId;
        bool syncingInputs, closing, readPending, hotkeyShow, hotkeyCycle, settingsOpen;
        bool mixerVisible = false;
        int refreshNumber;
        readonly ToolTip tips = new ToolTip();
        const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public MainForm(bool startInTray)
        {
            trayStart = startInTray; Text = "Audio Switcher Modern"; BackColor = Theme.Background; ForeColor = Theme.Text;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.Font(9, FontStyle.Regular); ClientSize = new Size(700, 460);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; KeyPreview = true;
            appIcon = CreateIcon(); Icon = appIcon;
            Panel header = new Panel { Dock = DockStyle.Top, Width = 700, Height = 76, Padding = new Padding(20, 12, 20, 10) };
            Label heading = Theme.Label("Audio", 21, Theme.Text); heading.SetBounds(20, 10, 200, 32); heading.Font = Theme.Font(18, FontStyle.Bold);
            Label caption = Theme.Label("YOUR SOUND, ONE PLACE", 8, Theme.Dim); caption.SetBounds(22, 47, 240, 19);
            pin = Theme.Button(prefs.Pinned ? "Pinned" : "Pin"); pin.SetBounds(568, 20, 54, 30); pin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Button hide = Theme.Button("x"); hide.Font = Theme.Font(11, FontStyle.Regular); hide.SetBounds(636, 20, 32, 30); hide.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.AddRange(new Control[] { heading, caption, pin, hide });
            header.MouseDown += DragWindow; heading.MouseDown += DragWindow; caption.MouseDown += DragWindow;
            pin.Click += delegate { prefs.Pinned = !prefs.Pinned; pin.Text = prefs.Pinned ? "Pinned" : "Pin"; Save(); };
            hide.Click += delegate { Hide(); };
            tips.SetToolTip(pin, "Keep the popup open when you click elsewhere"); tips.SetToolTip(hide, "Hide to system tray; right-click the tray icon to exit");

            Panel footer = new Panel { Dock = DockStyle.Bottom, Width = 700, Height = 46, Padding = new Padding(20, 4, 20, 8) };
            Label shortcut = Theme.Label("Alt + Shift + A  ·  open", 8, Theme.Dim); shortcut.SetBounds(20, 8, 220, 25);
            Button settings = Theme.Button("Settings"); settings.SetBounds(594, 6, 86, 30); settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            footer.Controls.AddRange(new Control[] { shortcut, settings }); settings.Click += delegate { ShowSettings(settings); };

            body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0), AutoScroll = true, BackColor = Theme.Background };
            TableLayoutPanel stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Theme.Background };
            contentStack = stack;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TableLayoutPanel columns = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Theme.Background };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            columns.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            TableLayoutPanel deviceColumn = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Dock = DockStyle.Top, Margin = new Padding(0, 0, 12, 0), Padding = Padding.Empty, BackColor = Theme.Background };
            TableLayoutPanel volumeColumn = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Dock = DockStyle.Top, Margin = new Padding(12, 0, 0, 0), Padding = Padding.Empty, BackColor = Theme.Background };
            deviceColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            volumeColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            columns.Controls.Add(deviceColumn, 0, 0); columns.Controls.Add(volumeColumn, 1, 0);
            AddToStack(stack, columns);
            AddSection(deviceColumn, "OUTPUT DEVICES", null);
            outputs = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false, Height = 104, AutoScroll = false, Dock = DockStyle.Fill, Margin = Padding.Empty };
            outputs.SizeChanged += delegate { FitRows(outputs, 48); };
            AddToStack(deviceColumn, outputs);
            AddSection(volumeColumn, "VOLUME", null);
            master = new VolumeRow("Output volume"); AddToStack(volumeColumn, master);
            master.Changed += delegate(int? volume, bool? mute) { string id = masterId; if (id != null) Command("device-" + id + (volume.HasValue ? "-level" : "-mute"), delegate { CoreAudio.SetDeviceVolume(id, volume, mute); }); };
            AddSection(deviceColumn, "MICROPHONE", null);
            inputs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Theme.Font(9.5f, FontStyle.Regular), Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 0, 0, 8), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 25, DisplayMember = "Name", AccessibleName = "Default microphone" };
            inputs.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                e.DrawBackground(); if (e.Index >= 0) { DeviceInfo d = inputs.Items[e.Index] as DeviceInfo; TextRenderer.DrawText(e.Graphics, d == null ? "No microphone connected" : d.Name, inputs.Font, e.Bounds, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); } e.DrawFocusRectangle();
            };
            inputs.SelectedIndexChanged += delegate { if (!syncingInputs && inputs.SelectedItem is DeviceInfo) { DeviceInfo selected = (DeviceInfo)inputs.SelectedItem; Command(null, delegate { CoreAudio.SetDefault(selected.Id, true); }); } };
            AddToStack(deviceColumn, inputs);
            AddSection(volumeColumn, "MICROPHONE LEVEL", null);
            microphone = new VolumeRow("Input level"); AddToStack(volumeColumn, microphone);
            microphone.Changed += delegate(int? volume, bool? mute) { string id = micId; if (id != null) Command("device-" + id + (volume.HasValue ? "-level" : "-mute"), delegate { CoreAudio.SetDeviceVolume(id, volume, mute); }); };
            mixer = Theme.Button("Show"); mixer.Width = 84; mixer.Height = 24;
            AddSection(stack, "APP MIXER", mixer);
            sessions = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false, Height = 0, AutoScroll = false, Visible = false, Dock = DockStyle.Fill, Margin = Padding.Empty };
            sessions.SizeChanged += delegate { FitRows(sessions, 70); };
            mixer.Click += delegate { mixerVisible = !mixerVisible; sessions.Visible = mixerVisible; mixer.Text = mixerVisible ? "Hide" : "Show"; ResizeForMixer(); };
            AddToStack(stack, sessions);
            status = Theme.Label("Connecting to Windows audio…", 8, Theme.Dim); status.Height = 30; status.Margin = new Padding(0, 5, 0, 0); AddToStack(stack, status);
            body.Controls.Add(stack); Controls.Add(body); Controls.Add(footer); Controls.Add(header);

            tray = new NotifyIcon { Icon = appIcon, Text = "Audio Switcher Modern", Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { if (Visible && ContainsFocus) Hide(); else Reveal(); } };
            ContextMenuStrip menu = NewMenu(); menu.Items.Add("Open", null, delegate { Reveal(); });
            menu.Items.Add("Next favourite output", null, delegate { Cycle(); }); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { closing = true; Close(); }); tray.ContextMenuStrip = menu;
            settingsMenu = NewMenu();
            settingsMenu.Closed += delegate { settingsOpen = false; };
            poll.Interval = 900; poll.Tick += delegate { if (Visible) RefreshAudio(); }; poll.Start();
            Deactivate += delegate { if (!closing && !Disposing && !prefs.Pinned && !settingsOpen && !inputs.DroppedDown && !menu.IsDisposed && !menu.Visible) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } };
        }
        void ResizeForMixer()
        {
            if (!IsHandleCreated || contentStack == null) return;
            // Size from the laid-out controls, never a base height plus mixer height.
            // Hidden mixer rows contribute nothing; session/device count changes resize too.
            contentStack.PerformLayout();
            int contentHeight = contentStack.GetPreferredSize(new Size(body.ClientSize.Width - body.Padding.Horizontal, 0)).Height;
            int chromeHeight = ClientSize.Height - body.ClientSize.Height;
            int desired = chromeHeight + contentHeight + body.Padding.Vertical + 10;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int target = Math.Min(desired, area.Height - 24);
            if (ClientSize.Height == target) return;
            int bottom = Bottom;
            ClientSize = new Size(ClientSize.Width, target);
            Top = Math.Max(area.Top + 12, Math.Min(bottom - Height, area.Bottom - Height - 12));
        }
        void AddToStack(TableLayoutPanel stack, Control control)
        {
            int row = stack.RowCount++; stack.RowStyles.Add(new RowStyle(SizeType.AutoSize)); control.Dock = DockStyle.Fill; stack.Controls.Add(control, 0, row);
        }
        void AddSection(TableLayoutPanel stack, string title, Button action)
        {
            Panel section = new Panel { Height = 32, Margin = Padding.Empty };
            Label label = Theme.Label(title, 8, Theme.Dim); label.Dock = DockStyle.Fill;
            if (action != null) { action.Dock = DockStyle.Right; section.Controls.Add(action); }
            section.Controls.Add(label); label.BringToFront(); AddToStack(stack, section);
        }
        void FitRows(FlowLayoutPanel container, int height)
        {
            // Lists grow to fit; the overall popup can scroll only if content exceeds the screen.
            foreach (Control control in container.Controls) { control.Width = Math.Max(100, container.ClientSize.Width - 3); control.Height = height; }
        }
        void DragWindow(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } }
        void Save() { try { prefs.Save(); } catch (Exception ex) { Preferences.Log(ex); SetStatus("Could not save settings: " + ex.Message, true); } }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); hotkeyShow = RegisterHotKey(Handle, 1, 0x4005, (uint)Keys.A); hotkeyCycle = RegisterHotKey(Handle, 2, 0x4005, (uint)Keys.S);
            try { int rounded = 2; DwmSetWindowAttribute(Handle, 33, ref rounded, 4); } catch (DllNotFoundException) { }
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); Place(); RefreshAudio(); if (trayStart) Hide();
        }
        void Place()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Height = Math.Min(Height, area.Height - 16); Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        }
        void Reveal() { if (!Visible) { Place(); Show(); } WindowState = FormWindowState.Normal; Activate(); RefreshAudio(); }
        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == Program.ShowMessage) { Reveal(); return; }
            if (m.Msg == 0x0312) { if (m.WParam.ToInt32() == 1) { if (Visible && ContainsFocus) Hide(); else Reveal(); } else if (m.WParam.ToInt32() == 2) Cycle(); return; }
            base.WndProc(ref m);
        }
        void OnUI(Action action)
        { if (!IsDisposed && IsHandleCreated) { try { BeginInvoke(action); } catch (InvalidOperationException) { } } }
        void RefreshAudio()
        {
            if (readPending || closing) return; readPending = true;
            worker.Post("read", delegate {
                AudioSnapshot result = CoreAudio.Read();
                OnUI(delegate { readPending = false; Apply(result); });
            });
        }
        void Command(string key, Action action)
        {
            worker.Post(key, delegate {
                try { action(); }
                catch (Exception ex) { Preferences.Log(ex); OnUI(delegate { SetStatus("Audio change failed: " + ex.Message, true); }); }
            });
        }
        void Apply(AudioSnapshot snapshot)
        {
            if (snapshot.Error != null) { SetStatus("Windows audio: " + snapshot.Error, true); return; }
            string signature = String.Join("|", snapshot.Devices.Select(d => d.Id + ":" + d.Name).ToArray());
            if (signature != deviceSignature) {
                deviceSignature = signature; outputs.SuspendLayout(); DisposeChildren(outputs); deviceButtons.Clear();
                foreach (DeviceInfo device in snapshot.Devices.Where(d => !d.Input)) {
                    DeviceButton button = new DeviceButton(device) { Favourite = prefs.Favourites.Contains(device.Id), Margin = new Padding(0, 0, 0, 3) };
                    button.Click += delegate { bool comms = prefs.Communications; Command(null, delegate { CoreAudio.SetDefault(device.Id, comms); }); };
                    button.FavouriteClicked += delegate { if (prefs.Favourites.Contains(device.Id)) prefs.Favourites.Remove(device.Id); else prefs.Favourites.Add(device.Id); button.Favourite = prefs.Favourites.Contains(device.Id); button.Invalidate(); Save(); };
                    ContextMenuStrip deviceMenu = NewMenu(); ToolStripMenuItem favourite = new ToolStripMenuItem("Favourite for quick switching");
                    deviceMenu.Opening += delegate { favourite.Checked = prefs.Favourites.Contains(device.Id); };
                    favourite.Click += delegate { if (prefs.Favourites.Contains(device.Id)) prefs.Favourites.Remove(device.Id); else prefs.Favourites.Add(device.Id); button.Favourite = prefs.Favourites.Contains(device.Id); button.Invalidate(); Save(); };
                    deviceMenu.Items.Add(favourite); button.ContextMenuStrip = deviceMenu; button.Disposed += delegate { deviceMenu.Dispose(); };
                    outputs.Controls.Add(button); deviceButtons.Add(button); tips.SetToolTip(button, device.Name + "\nClick to switch output. Star or right-click to favourite.");
                }
                if (deviceButtons.Count == 0) outputs.Controls.Add(Theme.Label("No output device connected", 9, Theme.Dim));
                outputs.Height = Math.Max(48, deviceButtons.Count * 51); FitRows(outputs, 48); outputs.ResumeLayout();
                syncingInputs = true; inputs.Items.Clear(); foreach (DeviceInfo input in snapshot.Devices.Where(d => d.Input)) inputs.Items.Add(input);
                inputs.Enabled = inputs.Items.Count > 0; syncingInputs = false;
            }
            foreach (DeviceButton button in deviceButtons) { DeviceInfo d = snapshot.Devices.FirstOrDefault(x => x.Id == button.Device.Id); if (d != null) { button.Device = d; button.Invalidate(); } }
            DeviceInfo output = snapshot.Devices.FirstOrDefault(d => !d.Input && d.IsDefault);
            DeviceInfo mic = snapshot.Devices.FirstOrDefault(d => d.Input && d.IsDefault);
            masterId = output == null ? null : output.Id; master.Enabled = output != null; if (output != null) master.Sync(output.Volume, output.Muted);
            micId = mic == null ? null : mic.Id; microphone.Enabled = mic != null; if (mic != null) microphone.Sync(mic.Volume, mic.Muted);
            if (!inputs.DroppedDown) {
                syncingInputs = true; int selectedIndex = -1;
                for (int i = 0; i < inputs.Items.Count; i++) if (((DeviceInfo)inputs.Items[i]).Id == micId) selectedIndex = i;
                inputs.SelectedIndex = selectedIndex; syncingInputs = false;
            }
            string sessionKey = String.Join("|", snapshot.Sessions.Select(s => s.Id + ":" + s.Name).ToArray());
            if (sessionKey != sessionSignature || refreshNumber++ == 0) {
                sessionSignature = sessionKey; sessions.SuspendLayout(); DisposeChildren(sessions); sessionRows.Clear();
                foreach (SessionInfo session in snapshot.Sessions.OrderBy(s => s.Name)) {
                    string label = session.Name;
                    if (snapshot.Sessions.Count(s => s.Name == session.Name) > 1) label += " · session " + (snapshot.Sessions.Where(s => s.Name == session.Name).ToList().IndexOf(session) + 1);
                    VolumeRow row = new VolumeRow(label); row.Changed += delegate(int? volume, bool? mute) {
                        Command("session-" + session.Id + (volume.HasValue ? "-level" : "-mute"), delegate { CoreAudio.SetSessionVolume(session.DeviceId, session.Id, volume, mute); });
                    };
                    sessions.Controls.Add(row); sessionRows[session.Id] = row;
                }
                if (snapshot.Sessions.Count == 0) { Label empty = Theme.Label("Play audio to see apps on this output.", 9, Theme.Dim); empty.Height = 70; sessions.Controls.Add(empty); }
                sessions.Height = Math.Max(85, snapshot.Sessions.Count * 85);
                FitRows(sessions, 70); sessions.ResumeLayout();
            }
            foreach (SessionInfo session in snapshot.Sessions) { VolumeRow row; if (sessionRows.TryGetValue(session.Id, out row)) row.Sync(session.Volume, session.Muted); }
            if (status.Tag == null || (DateTime.UtcNow - (DateTime)status.Tag).TotalSeconds > 7) {
                string note = "Devices on the left · Volume on the right";
                if (!hotkeyShow || !hotkeyCycle) note = "A shortcut is in use by another app; tray controls work.";
                if (snapshot.Warning != null) SetStatus(snapshot.Warning, true); else SetStatus(note, false);
            }
            ResizeForMixer();
            tray.Text = "Audio Switcher Modern" + (output == null ? "" : " · " + output.Volume + "%" + (output.Muted ? " (muted)" : ""));
        }
        void Cycle()
        {
            // Refresh first so disconnected Bluetooth/USB devices and outside changes are respected.
            string[] favourites = prefs.Favourites.ToArray(); bool comms = prefs.Communications;
            worker.Post("cycle", delegate {
                try {
                    AudioSnapshot snapshot = CoreAudio.Read(); if (snapshot.Error != null) throw new InvalidOperationException(snapshot.Error);
                    List<DeviceInfo> choices = snapshot.Devices.Where(d => !d.Input && (favourites.Length == 0 || favourites.Contains(d.Id))).ToList();
                    if (choices.Count == 0) { OnUI(delegate { SetStatus("No favourite outputs are connected. Star an available device.", true); }); return; }
                    int index = choices.FindIndex(d => d.IsDefault); DeviceInfo next = choices[(index + 1) % choices.Count]; CoreAudio.SetDefault(next.Id, comms);
                    OnUI(delegate { RefreshAudio(); });
                } catch (Exception ex) { Preferences.Log(ex); OnUI(delegate { SetStatus("Switch failed: " + ex.Message, true); }); }
            });
        }
        static void DisposeChildren(Control control) { while (control.Controls.Count > 0) control.Controls[0].Dispose(); }
        void SetStatus(string text, bool error) { status.Text = text; status.ForeColor = error ? Color.FromArgb(255, 178, 152) : Theme.Dim; status.Tag = error ? (object)DateTime.UtcNow : null; tips.SetToolTip(status, text); }
        ContextMenuStrip NewMenu() { return new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false, Font = Theme.Font(9, FontStyle.Regular) }; }
        void ShowSettings(Control anchor)
        {
            ContextMenuStrip menu = settingsMenu;
            while (menu.Items.Count > 0) menu.Items[0].Dispose();
            ToolStripMenuItem auto = new ToolStripMenuItem("Start with Windows");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath)) auto.Checked = key != null && key.GetValue("AudioSwitcherModern") != null;
            auto.Click += delegate {
                try { using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath)) {
                    if (auto.Checked) key.DeleteValue("AudioSwitcherModern", false); else key.SetValue("AudioSwitcherModern", "\"" + Application.ExecutablePath + "\" --tray");
                } } catch (Exception ex) { SetStatus("Startup setting failed: " + ex.Message, true); }
            };
            ToolStripMenuItem communications = new ToolStripMenuItem("Switch communications output too") { Checked = prefs.Communications };
            communications.Click += delegate { prefs.Communications = !prefs.Communications; Save(); };
            menu.Items.Add(auto); menu.Items.Add(communications); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Next favourite · Alt+Shift+S", null, delegate { Cycle(); });
            menu.Items.Add("Windows sound settings", null, delegate { try { Process.Start("ms-settings:sound"); } catch (Exception ex) { SetStatus(ex.Message, true); } });
            menu.Items.Add("Exit", null, delegate { closing = true; Close(); });
            // Keep the popup alive while a settings menu is open.
            settingsOpen = true;
            menu.Show(anchor, new Point(0, -menu.GetPreferredSize(Size.Empty).Height));
        }
        static Icon CreateIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32)) using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                using (Brush b = new SolidBrush(Theme.Accent)) using (GraphicsPath path = Theme.Round(new RectangleF(0, 0, 31, 31), 8)) g.FillPath(b, path);
                using (Pen p = new Pen(Theme.Background, 3)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, 9, 13, 9, 19); g.DrawLine(p, 16, 8, 16, 24); g.DrawLine(p, 23, 11, 23, 21); }
                IntPtr handle = bmp.GetHicon(); try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); } finally { DestroyIcon(handle); }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { poll.Stop(); poll.Dispose(); UnregisterHotKey(Handle, 1); UnregisterHotKey(Handle, 2); tray.Visible = false; tray.ContextMenuStrip.Dispose(); settingsMenu.Dispose(); tray.Dispose(); tips.Dispose(); worker.Dispose(); appIcon.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
