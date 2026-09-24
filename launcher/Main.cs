// THE DUMB LAUNCHER: the changelog, an UPDATE button and PLAY, and nothing else.
//
// THE REAL MAIN MENU STAYS IN THE GAME. It is a live diorama with character creation and the whole
// join UI in it; a second copy of that here would be two UIs to keep in step, which is the thing
// this design exists to avoid.
//
// THIS FILE CONTAINS NO DECISIONS. Every branch below is a BuildOutcome that scripts/Builds.cs
// already decided, switched on to set two Enabled flags and one label -- which is why the smoke
// test proving Builds.cs proves this program too. If something here ever seems to need an `if` of
// its own, it belongs in Builds.cs (where the ladder can see it) or in tools/pack.ps1 (where it can
// be changed). THIS PROGRAM CAN NEVER BE CHANGED ONCE IT IS IN PLAYERS' HANDS -- there is nothing
// to update it. That is the constraint everything here is shaped by.
//
// THE TWO HEDGES, both of which have to be in v1 or "frozen" is a wish rather than a plan:
//   * BUILD.txt carries `schema`. A release numbered higher than Builds.Schema stops with a
//     message and a URL instead of breaking.
//   * `source.txt` beside this exe overrides the base URL, so the host can move -- and so a test
//     install can be pointed at a file:/// folder -- without a new launcher.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Warships.Launcher
{
    // The release over HTTPS. The GAME never carries System.Net.Http: IBuildSource is the seam, and
    // the checks implement it over a folder instead.
    internal sealed class HttpBuildSource : IBuildSource
    {
        private readonly string _base;
        private readonly HttpClient _http;

        public HttpBuildSource(string baseUrl)
        {
            _base = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
            // A folder can be a source too, which is what source.txt is for.
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("WarshipsLauncher/1");
        }

        public string Text(string name)
        {
            if (_base.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllText(new Uri(_base + name).LocalPath);
            return _http.GetStringAsync(_base + name).GetAwaiter().GetResult();
        }

        public Stream Open(string name)
        {
            if (_base.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return File.OpenRead(new Uri(_base + name).LocalPath);
            var r = _http.GetAsync(_base + name, HttpCompletionOption.ResponseHeadersRead)
                         .GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return r.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        }
    }

    internal sealed class Shell : Form
    {
        // Where releases live. Overridden by source.txt beside this exe. The `latest/download`
        // path is a permanent redirect to the newest release's asset of that name: no API call, no
        // token, no rate limit, and the asset NAMES are ours rather than GitHub's schema.
        private const string DefaultBase =
            "https://github.com/ArmosCodesStuff/MP_Space_Game/releases/latest/download/";

        // Environment.ProcessPath, not AppContext.BaseDirectory: under PublishSingleFile the latter
        // has meant the extraction folder, and writing an update there would update nothing.
        private readonly string _root =
            Path.GetDirectoryName(Environment.ProcessPath ?? Application.ExecutablePath);
        private readonly RichTextBox _notes = new RichTextBox();
        private readonly Label _status = new Label();
        private readonly ProgressBar _bar = new ProgressBar();
        private readonly Button _update = new Button();
        private readonly Button _play = new Button();

        private IBuildSource _src;
        private BuildInfo _local, _remote;
        private BuildPlan _plan;
        private string _launch;

        internal Shell()
        {
            Text = "Warships";
            ClientSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(18, 18, 20);
            MinimumSize = new Size(560, 400);

            _notes.ReadOnly = true;
            _notes.DetectUrls = false;
            // BOTH bars. The notes are hard-wrapped in the record, but at a width the record chose
            // and not one this window knows, so a line CAN be wider than the box -- and with no
            // horizontal bar the end of it is simply unreachable.
            _notes.ScrollBars = RichTextBoxScrollBars.Both;
            _notes.WordWrap = false;                       // wrapping hard-wrapped text reads worse
            _notes.Font = NoteFont;
            _notes.BackColor = Color.FromArgb(24, 24, 28);
            _notes.ForeColor = Plain;
            _notes.BorderStyle = BorderStyle.None;
            _notes.Dock = DockStyle.Fill;
            _notes.Text = "Checking for updates...";

            _status.Dock = DockStyle.Top;
            _status.Height = 26;
            _status.ForeColor = Color.FromArgb(180, 180, 190);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(4, 0, 0, 0);

            _bar.Dock = DockStyle.Bottom;
            _bar.Height = 6;
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Visible = false;

            var row = new Panel { Dock = DockStyle.Bottom, Height = 52 };
            foreach (var b in new[] { _update, _play })
            {
                b.Size = new Size(150, 36);
                b.FlatStyle = FlatStyle.Flat;
                b.ForeColor = Color.White;
                b.BackColor = Color.FromArgb(44, 44, 52);
                b.Enabled = false;
                row.Controls.Add(b);
            }
            _update.Text = "UPDATE";
            _play.Text = "PLAY";
            _play.BackColor = Color.FromArgb(38, 82, 56);
            row.Resize += (s, e) =>
            {
                _play.Location = new Point(row.Width - 162, 8);
                _update.Location = new Point(row.Width - 324, 8);
            };

            _update.Click += (s, e) => RunUpdate();
            _play.Click += (s, e) => Play();

            Controls.Add(_notes);
            Controls.Add(row);
            Controls.Add(_bar);
            Controls.Add(_status);

            Shown += (s, e) => Look();
        }

        // ── the decision, once, at startup ───────────────────────────────────
        private void Look()
        {
            // A journal on disk outranks everything: an update was interrupted between the download
            // and the swap, and the install is neither one build nor the other until it is finished.
            if (Builds.Resume(_root)) Say("finished an interrupted update");

            string baseUrl = DefaultBase;
            try
            {
                string over = Path.Combine(_root, Builds.SourceName);
                if (File.Exists(over)) baseUrl = File.ReadAllText(over).Trim();
            }
            catch (IOException) { }

            _src = new HttpBuildSource(baseUrl);
            _local = Read(() => Builds.ReadInfo(File.ReadAllText(Path.Combine(_root, Builds.InfoName))));
            _remote = Read(() => Builds.ReadInfo(_src.Text(Builds.InfoName)));

            string malformed = null;
            BuildDiff diff = null;
            if (_remote != null && _remote.Schema <= Builds.Schema)
            {
                var want = Read(() => Builds.ReadManifest(_src.Text(_remote.Manifest)));
                malformed = Builds.Validate(_remote, want);
                if (malformed == null && (_local == null || _local.Build != _remote.Build))
                {
                    var ignore = new[] { _remote.Manifest, Builds.InfoName, _remote.Notes };
                    diff = Builds.Compare(_root, want, ignore);
                    if (!diff.Same) _plan = Builds.Plan(_remote, diff);
                }
            }

            string say;
            BuildOutcome what = Builds.Decide(_root, _local, _remote, diff, malformed, out say);
            _launch = (_remote ?? _local)?.Launch;

            // EVERY ROW ENDS THE SAME WAY: PLAY stays on unless there is genuinely nothing to play.
            bool haveGame = _launch != null && File.Exists(Path.Combine(_root, _launch));
            _play.Enabled = haveGame;
            _update.Enabled = what == BuildOutcome.UpdateReady || what == BuildOutcome.NotInstalled;
            if (what == BuildOutcome.UpdateReady && _plan != null)
                say += string.Format(" -- {0:0.0} MB", _plan.Bytes / 1048576.0);
            if (what == BuildOutcome.LauncherTooOld) say += ": " + DefaultBase;
            Say(say);

            ShowNotes(Read(() => _src.Text((_remote ?? _local)?.Notes ?? "NOTES.txt"))
                      ?? "(no release notes -- " + say + ")");

            if (what == BuildOutcome.Interrupted) Look();          // it is finished now; ask again
        }

        // ── HOW THE NOTES ARE DRAWN ───────────────────────────────────────────────────────
        // A SECTION IS A HEADING IN SQUARE BRACKETS ON ITS OWN LINE, and tools\pack.ps1 writes
        // them. This end knows how to DRAW a section and nothing about which sections exist:
        // the brackets are stripped, the heading is drawn bold in its row colour, and the lines
        // under it take that row body colour until the next heading.
        //
        // AN UNKNOWN HEADING DRAWS PLAINLY rather than being refused, and that is the whole
        // point: a release can add a section and every launcher already in the world renders it
        // correctly without being replaced. Deciding WHAT the sections are stays on the repo
        // side of the line, where a mistake can be fixed.
        private sealed class NoteStyle
        {
            public string Head;        // the bracketed text, matched whole, ignoring case
            public Color Title, Body;
        }
        private static readonly Color Plain = Color.FromArgb(210, 210, 215);
        private static readonly Font NoteFont = new Font("Consolas", 9f);
        private static readonly Font NoteBold = new Font("Consolas", 9f, FontStyle.Bold);
        private static readonly NoteStyle[] Styles =
        {
            // The standing notice: amber, because it is the one part a player must ACT on.
            new NoteStyle { Head = "FIRST TIME", Title = Color.FromArgb(255, 190,  90),
                            Body = Color.FromArgb(235, 200, 150) },
            // The hard block, and the only section that asks a player to change their machine.
            // Red, because "this cannot be undone" has to be read before it is acted on.
            new NoteStyle { Head = "IF WINDOWS BLOCKS IT OUTRIGHT", Title = Color.FromArgb(255, 110, 100),
                            Body = Color.FromArgb(240, 180, 175) },
            // What actually changed: the heading stands out, the entry reads as ordinary text.
            new NoteStyle { Head = "GAME UPDATES", Title = Color.FromArgb(120, 200, 255),
                            Body = Plain },
        };
        private static NoteStyle StyleFor(string head)
        {
            foreach (var st in Styles)
                if (string.Equals(st.Head, head, StringComparison.OrdinalIgnoreCase)) return st;
            return new NoteStyle { Head = head, Title = Plain, Body = Plain };
        }

        private void ShowNotes(string text)
        {
            _notes.Clear();
            var body = Plain;
            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                bool head = line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']';
                if (head)
                {
                    var st = StyleFor(line.Substring(1, line.Length - 2).Trim());
                    body = st.Body;
                    Put(line.Substring(1, line.Length - 2).Trim(), st.Title, NoteBold);
                }
                else Put(raw, body, NoteFont);
            }
            _notes.SelectionStart = 0;
            _notes.ScrollToCaret();
        }

        private void Put(string line, Color colour, Font font)
        {
            _notes.SelectionStart = _notes.TextLength;
            _notes.SelectionLength = 0;
            _notes.SelectionColor = colour;
            _notes.SelectionFont = font;
            _notes.AppendText(line + Environment.NewLine);
        }

        // ── the update ───────────────────────────────────────────────────────
        private async void RunUpdate()
        {
            // Asked BEFORE a byte is downloaded. A live Windows image cannot be renamed over.
            if (_launch != null && Builds.Locked(Path.Combine(_root, _launch)))
            { Say("close the game before updating"); return; }

            _update.Enabled = false;
            _play.Enabled = false;
            _bar.Visible = true;
            try
            {
                BuildPlan plan = _plan;
                BuildInfo remote = _remote;
                IBuildSource src = _src;
                string root = _root;
                if (plan == null)                                   // a fresh install: take it all
                {
                    var want = Builds.ReadManifest(src.Text(remote.Manifest));
                    var ignore = new[] { remote.Manifest, Builds.InfoName, remote.Notes };
                    plan = Builds.Plan(remote, Builds.Compare(root, want, ignore));
                }
                await Task.Run(() => Builds.Apply(root, src, remote, plan, Progress));
                _bar.Visible = false;
                Say("updated to " + remote.Build);
                Look();
            }
            catch (Exception ex)
            {
                // ROWS 10, 11 AND 12 ALL LAND HERE, and all three leave the install exactly as it
                // was: nothing outside .staging is written until every byte has been verified.
                // There is no automatic retry -- a checksum that does not match is a bad release or
                // an interfering proxy, not a blip, and retrying turns one into a loop.
                _bar.Visible = false;
                Say("update failed: " + ex.Message + " -- the game you have is untouched");
                _update.Enabled = true;
                _play.Enabled = _launch != null && File.Exists(Path.Combine(_root, _launch));
            }
        }

        private void Progress(string what, double frac)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    _status.Text = what;
                    _bar.Value = Math.Max(0, Math.Min(100, (int)(frac * 100)));
                }));
            }
            catch (InvalidOperationException) { }
        }

        // ── play ─────────────────────────────────────────────────────────────
        private void Play()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(_root, _launch),
                    WorkingDirectory = _root,
                    UseShellExecute = false,
                });
                // EXIT rather than wait: the launcher must not be holding a handle in the folder
                // the next update has to replace.
                Application.Exit();
            }
            catch (Exception ex) { Say("could not start the game: " + ex.Message); }
        }

        private void Say(string s) { _status.Text = s; }

        private static T Read<T>(Func<T> f) where T : class
        {
            try { return f(); } catch (Exception) { return null; }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Spelled out rather than ApplicationConfiguration.Initialize(): that is generated by a
            // source generator, and a program that can never be rebuilt should not depend on one.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new Shell());
        }
    }
}
