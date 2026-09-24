// AN INSTALLED BUILD THAT CAN BE REPLACED FROM A SOURCE OF TRUTH.
//
// WHAT IT REPLACES: nothing, but it is easy to think otherwise. `version/MANIFEST.sha256` is the
// only per-file hash list this repo had, and it covers the SOURCE TREE (`git ls-files`, via
// tools/manifest.ps1) -- it names no .exe, no .pck and not one .dll. This file is that same idea
// aimed at what a player actually has on disk. The two manifests share a FORMAT and a generator,
// never a file.
//
// WHAT A NEW PART ROW MUST FILL IN: an id, an asset name, that asset's sha256, its size in bytes,
// and one `in=<id>|<path>` line per file it holds. That is the whole of it -- nothing in this file
// changes, because the launcher only ever expands the list it is handed and downloads the parts
// that hold a changed path. How the build is CUT into parts is a pack-tool decision; keeping it
// there is what lets the launcher stay frozen.
//
// NO `using Godot` IN THIS FILE, EVER. launcher/Launcher.csproj links it with
// <Compile Include="..\scripts\Builds.cs" />, and the launcher has no engine. Anything that needs
// OS, GD or ProjectSettings belongs in Game.cs, which owns the build's identity, not here.
//
// WHERE THE SAFETY LIVES: CheckRel() is the only judge of whether a manifest line is a path at all,
// and Resolve() is the only way one becomes a place to write. Between them nothing outside the
// install root can be touched -- which is what keeps `user://`
// (%APPDATA%\Godot\app_userdata\Warships, where every character lives) out of reach by geometry
// rather than by care. There is no delete of anything the release names: a file on disk that the
// release does not name is Extra -- REPORTED, NEVER REMOVED. The only recursive delete is of
// `.staging`, a folder this file created itself.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// A ROW OF THE RELEASE MANIFEST: one file, and the hash it must have.
public sealed class BuildEntry
{
    public string Path;
    public string Sha;
}

// A ROW OF THE PART TABLE: one downloadable archive, and every path it holds.
public sealed class BuildPart
{
    public string Id;
    public string Asset;
    public string Sha;
    public long Bytes;
    public List<string> Paths = new List<string>();
}

// WHAT A RELEASE SAYS ABOUT ITSELF -- the parsed BUILD.txt.
public sealed class BuildInfo
{
    public int Schema;
    public string Build;
    public string Launch;
    public string Notes;
    public string Manifest;
    public List<BuildPart> Parts = new List<BuildPart>();
    public BuildPart PartOf(string path) =>
        Parts.FirstOrDefault(p => p.Paths.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)));
}

// WHAT THE INSTALL HAS, against what a release says it should have.
public sealed class BuildDiff
{
    public List<BuildEntry> Wrong = new List<BuildEntry>();     // present, wrong hash
    public List<BuildEntry> Missing = new List<BuildEntry>();   // not present at all
    public List<string> Extra = new List<string>();             // present, unnamed -- reported, never removed
    public bool Same => Wrong.Count == 0 && Missing.Count == 0;
}

// WHAT AN UPDATE WOULD DO. Making one writes nothing.
public sealed class BuildPlan
{
    public List<BuildPart> Parts = new List<BuildPart>();
    public List<BuildEntry> Files = new List<BuildEntry>();
    public long Bytes;
}

// THE DECISION TABLE, as one value. Every row of it in docs/DESIGN.md ends in one of these.
public enum BuildOutcome
{
    Interrupted,        // a journal is on disk: finish it before anything else
    Offline,            // the release could not be reached; play what is installed
    LauncherTooOld,     // the release's schema is newer than this program knows
    Malformed,          // the release contradicts itself; play what is installed
    NotInstalled,       // nothing to launch; updating is the only choice
    UpToDate,           // the installed build is the published one
    UpdateReady,        // there is something to replace
}

// WHERE A RELEASE'S BYTES COME FROM. The seam that keeps System.Net.Http out of the game: the
// launcher implements this over HTTPS, the checks and a `file:///` test install over a folder.
public interface IBuildSource
{
    string Text(string name);
    Stream Open(string name);
}

// A RELEASE IN A FOLDER. What a rung-3 check points at, and what `source.txt` makes possible
// without shipping a new launcher.
public sealed class FolderBuildSource : IBuildSource
{
    private readonly string _dir;
    public FolderBuildSource(string dir) { _dir = dir; }
    public string Text(string name) => File.ReadAllText(System.IO.Path.Combine(_dir, name));
    public Stream Open(string name) => File.OpenRead(System.IO.Path.Combine(_dir, name));
}

public static class Builds
{
    // THE FROZEN CONTRACT. A release naming a higher schema is refused with a message, never
    // guessed at -- that refusal is the only reason a launcher can be shipped and never rebuilt.
    public const int Schema = 1;
    public const string InfoName = "BUILD.txt";
    public const string SourceName = "source.txt";
    public const string StageDir = ".staging";

    private const string Journal = "JOURNAL";
    private const string Staged = "files";

    // ── paths ────────────────────────────────────────────────────────────────
    // A manifest line is TEXT FROM A DOWNLOAD. It is a path only if it passes here: a plain
    // relative path, forward slashes, no drive, no root, no `..`. Nothing is expanded -- a line
    // reading `%APPDATA%/x` names a folder called `%APPDATA%` inside the install and reaches
    // nothing of the player's.
    public static void CheckRel(string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) throw new InvalidDataException("a manifest path is empty");
        if (rel.IndexOf('\\') >= 0) throw new InvalidDataException("a manifest path has a backslash: " + rel);
        if (rel.IndexOf(':') >= 0) throw new InvalidDataException("a manifest path has a drive or scheme: " + rel);
        if (rel[0] == '/') throw new InvalidDataException("a manifest path is rooted: " + rel);
        foreach (string seg in rel.Split('/'))
            if (seg.Length == 0 || seg == "." || seg == "..")
                throw new InvalidDataException("a manifest path escapes the install: " + rel);
    }

    // THE ONLY WAY A PATH BECOMES A PLACE TO WRITE.
    public static string Resolve(string root, string rel)
    {
        CheckRel(rel);
        string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, rel));
        string bas = System.IO.Path.GetFullPath(root);
        if (bas[bas.Length - 1] != System.IO.Path.DirectorySeparatorChar) bas += System.IO.Path.DirectorySeparatorChar;
        if (full.Length <= bas.Length || !full.StartsWith(bas, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("a manifest path escapes the install: " + rel);
        return full;
    }

    public static string HashFile(string path)
    {
        using (var sha = SHA256.Create())
        using (var f = File.OpenRead(path))
            return Hex(sha.ComputeHash(f));
    }

    private static string Hex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (byte x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }

    // ── reading what a release says ──────────────────────────────────────────
    // sha256sum's own format and nothing else: lower-case hash, TWO spaces, forward slashes. Kept
    // strict on purpose -- `sha256sum -c BUILD.sha256` from Git Bash has to check an install by
    // hand, with no launcher in the way, exactly as tools/manifest.ps1 relies on today. A BOM is
    // stripped because it makes the first line unparseable to sha256sum as well as to this.
    public static List<BuildEntry> ReadManifest(string text)
    {
        var list = new List<BuildEntry>();
        if (string.IsNullOrEmpty(text)) return list;
        if (text[0] == '﻿') text = text.Substring(1);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (line.Length < 67 || line.IndexOf("  ", StringComparison.Ordinal) != 64)
                throw new InvalidDataException("not a sha256sum line: " + line);
            var e = new BuildEntry { Sha = line.Substring(0, 64).ToLowerInvariant(), Path = line.Substring(66) };
            CheckRel(e.Path);
            list.Add(e);
        }
        return list;
    }

    // BUILD.txt: `key=value` lines, unknown keys ignored so a later release can add one without
    // this refusing it. Deliberately not JSON -- a program that can never be rebuilt should not
    // carry a parser whose behaviour it cannot change, and the game's own saves are already INI.
    public static BuildInfo ReadInfo(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var info = new BuildInfo();
        var by = new Dictionary<string, BuildPart>(StringComparer.Ordinal);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string k = line.Substring(0, eq), v = line.Substring(eq + 1);
            if (k == "schema") { int s; info.Schema = int.TryParse(v, out s) ? s : 0; }
            else if (k == "build") info.Build = v;
            else if (k == "launch") info.Launch = v;
            else if (k == "notes") info.Notes = v;
            else if (k == "manifest") info.Manifest = v;
            else if (k == "part")
            {
                string[] f = v.Split('|');
                if (f.Length != 4) throw new InvalidDataException("a part row wants id|asset|sha|bytes: " + v);
                long b;
                var p = new BuildPart { Id = f[0], Asset = f[1], Sha = f[2].ToLowerInvariant(), Bytes = long.TryParse(f[3], out b) ? b : 0 };
                info.Parts.Add(p);
                by[p.Id] = p;
            }
            else if (k == "in")
            {
                string[] f = v.Split('|');
                if (f.Length != 2) throw new InvalidDataException("an in row wants partid|path: " + v);
                BuildPart p;
                if (!by.TryGetValue(f[0], out p)) throw new InvalidDataException("no part is named " + f[0]);
                p.Paths.Add(f[1]);
            }
        }
        return info;
    }

    // EVERYTHING THE LAUNCHER ASSUMES, ASKED ONCE. Returns null when the release holds together,
    // or the one sentence to show the player when it does not.
    public static string Validate(BuildInfo info, List<BuildEntry> want)
    {
        if (info == null) return "the release did not say what it is";
        if (info.Schema <= 0) return "the release has no schema";
        if (string.IsNullOrEmpty(info.Build)) return "the release has no build id";
        if (string.IsNullOrEmpty(info.Launch)) return "the release does not say what to launch";
        if (string.IsNullOrEmpty(info.Manifest)) return "the release does not say where its manifest is";
        if (info.Parts.Count == 0) return "the release has no parts";
        if (want == null || want.Count == 0) return "the release has no manifest";
        foreach (BuildEntry e in want)
            if (info.PartOf(e.Path) == null) return "no part holds " + e.Path;
        if (!want.Any(e => string.Equals(e.Path, info.Launch, StringComparison.OrdinalIgnoreCase)))
            return "the manifest does not name " + info.Launch;
        return null;
    }

    // ── what the install has ─────────────────────────────────────────────────
    public static BuildDiff Compare(string root, List<BuildEntry> want, IEnumerable<string> ignore)
    {
        var d = new BuildDiff();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string s in ignore ?? Enumerable.Empty<string>()) if (s != null) named.Add(s);
        foreach (BuildEntry e in want)
        {
            named.Add(e.Path);
            string full = Resolve(root, e.Path);
            if (!File.Exists(full)) { d.Missing.Add(e); continue; }
            if (!string.Equals(HashFile(full), e.Sha, StringComparison.OrdinalIgnoreCase)) d.Wrong.Add(e);
        }
        // EXTRA IS A REPORT, NOT A WORK LIST. A player's screenshot, a mod, a shortcut: an update
        // that tidies the folder it was given is an update that eats something one day.
        string bas = System.IO.Path.GetFullPath(root);
        if (Directory.Exists(bas))
            foreach (string f in Directory.EnumerateFiles(bas, "*", SearchOption.AllDirectories))
            {
                string rel = System.IO.Path.GetRelativePath(bas, f).Replace('\\', '/');
                if (rel.StartsWith(StageDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(".old", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel == SourceName) continue;
                if (!named.Contains(rel)) d.Extra.Add(rel);
            }
        return d;
    }

    // ONLY THE PARTS THAT HOLD SOMETHING CHANGED. This is the whole point: five files of 189 move
    // on an ordinary release, so one 3 MB asset is fetched and the other 70 MB is not.
    public static BuildPlan Plan(BuildInfo info, BuildDiff diff)
    {
        var plan = new BuildPlan();
        foreach (BuildEntry e in diff.Missing.Concat(diff.Wrong))
        {
            BuildPart p = info.PartOf(e.Path);
            if (p == null) throw new InvalidDataException("no part holds " + e.Path);
            if (!plan.Parts.Contains(p)) { plan.Parts.Add(p); plan.Bytes += p.Bytes; }
            plan.Files.Add(e);
        }
        return plan;
    }

    // ── the decision ─────────────────────────────────────────────────────────
    // THE TABLE, in order. It touches the disk only to ask whether two paths exist, so a check can
    // put any row of it in front of this with literals. `diff` may be null when the build ids
    // already agree or the release was unreachable; it is required otherwise.
    public static BuildOutcome Decide(string root, BuildInfo local, BuildInfo remote,
                                      BuildDiff diff, string malformed, out string say)
    {
        if (File.Exists(System.IO.Path.Combine(root, StageDir, Journal)))
        { say = "finishing an interrupted update"; return BuildOutcome.Interrupted; }

        if (remote == null)
        { say = "offline -- cannot check for updates"; return BuildOutcome.Offline; }

        if (remote.Schema > Schema)
        { say = "this release needs a newer launcher"; return BuildOutcome.LauncherTooOld; }

        if (malformed != null)
        { say = "this release is malformed: " + malformed; return BuildOutcome.Malformed; }

        bool installed = local != null
                      && !string.IsNullOrEmpty(local.Launch)
                      && File.Exists(System.IO.Path.Combine(root, local.Launch))
                      && !string.IsNullOrEmpty(local.Manifest)
                      && File.Exists(System.IO.Path.Combine(root, local.Manifest));
        if (!installed)
        { say = "the game is not installed -- press UPDATE"; return BuildOutcome.NotInstalled; }

        if (string.Equals(local.Build, remote.Build, StringComparison.Ordinal))
        { say = "up to date (" + local.Build + ")"; return BuildOutcome.UpToDate; }

        // IDS ARE COMPARED, NEVER ORDERED. A differing id is a reason to look, not to download --
        // which is also what makes re-publishing an older build work with no extra code.
        if (diff != null && diff.Same)
        { say = "already up to date"; return BuildOutcome.UpToDate; }

        int n = diff == null ? 0 : diff.Wrong.Count + diff.Missing.Count;
        say = "update available: " + remote.Build + " (" + n + " files)";
        return BuildOutcome.UpdateReady;
    }

    // THE GAME IS RUNNING? A live Windows image is locked against write and delete, so asking for
    // write access is the honest question. Asked BEFORE anything is downloaded.
    public static bool Locked(string path)
    {
        if (!File.Exists(path)) return false;
        try { using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    // ── doing it ─────────────────────────────────────────────────────────────
    // Nothing outside `.staging` is written until the journal exists. Every archive is hashed
    // against BUILD.txt before it is opened and every extracted file against BUILD.sha256 before
    // it is eligible, so a download that is cut off, corrupted or intercepted cannot reach the
    // install at all -- it can only fail, and a failure still leaves a game that runs.
    public static void Apply(string root, IBuildSource src, BuildInfo remote,
                             BuildPlan plan, Action<string, double> say)
    {
        string stage = System.IO.Path.Combine(root, StageDir);
        string files = System.IO.Path.Combine(stage, Staged);
        Wipe(stage);
        Directory.CreateDirectory(files);
        try
        {
            foreach (BuildPart p in plan.Parts)                              // 2 FETCH
            {
                if (say != null) say("downloading " + p.Asset, 0);
                string part = System.IO.Path.Combine(stage, p.Asset + ".part");
                using (Stream s = src.Open(p.Asset))
                using (FileStream f = File.Create(part)) s.CopyTo(f);
                string got = HashFile(part);
                if (!string.Equals(got, p.Sha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(p.Asset + " did not match its checksum");
                File.Move(part, System.IO.Path.Combine(stage, p.Asset));
            }

            int n = 0;                                                       // 3 EXTRACT
            foreach (var g in plan.Files.GroupBy(e => remote.PartOf(e.Path)))
                using (ZipArchive z = ZipFile.OpenRead(System.IO.Path.Combine(stage, g.Key.Asset)))
                    foreach (BuildEntry e in g)
                    {
                        ZipArchiveEntry entry = z.GetEntry(e.Path);
                        if (entry == null) throw new InvalidDataException(g.Key.Asset + " does not hold " + e.Path);
                        string dst = Resolve(files, e.Path);
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dst));
                        entry.ExtractToFile(dst, true);
                        if (!string.Equals(HashFile(dst), e.Sha, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(e.Path + " did not match its checksum");
                        if (say != null) say("checking " + e.Path, ++n / (double)plan.Files.Count);
                    }

            foreach (string d in Descriptors(remote))                        // the three text files
                File.WriteAllText(Resolve(files, d), src.Text(d));

            // 4 JOURNAL -- THE COMMIT POINT. Written only once every staged file has been verified,
            // so everything it names is known-good and a resume can roll FORWARD with no rollback
            // path to get wrong. The descriptors go last: if the machine dies mid-swap the ids on
            // disk still name the OLD build, so the next run redoes the work rather than believing
            // a lie.
            var sb = new StringBuilder();
            sb.Append("build=").Append(remote.Build).Append('\n');
            foreach (BuildEntry e in plan.Files) sb.Append("pair=").Append(e.Path).Append('\n');
            foreach (string d in Descriptors(remote)) sb.Append("pair=").Append(d).Append('\n');
            File.WriteAllText(System.IO.Path.Combine(stage, Journal), sb.ToString());
        }
        catch { Wipe(stage); throw; }
        Swap(root);                                                          // 5 SWAP, 6 FINISH
    }

    // A JOURNAL FOUND AT STARTUP. True when there was one, whatever became of it.
    public static bool Resume(string root)
    {
        if (!File.Exists(System.IO.Path.Combine(root, StageDir, Journal))) return false;
        Swap(root);
        return true;
    }

    private static IEnumerable<string> Descriptors(BuildInfo i)
    {
        yield return i.Manifest;
        yield return InfoName;
        if (!string.IsNullOrEmpty(i.Notes)) yield return i.Notes;
    }

    // Renames only, on one volume: a file is the old one or the new one, never a half of either.
    private static void Swap(string root)
    {
        string stage = System.IO.Path.Combine(root, StageDir);
        string files = System.IO.Path.Combine(stage, Staged);
        string j = System.IO.Path.Combine(stage, Journal);
        string[] lines = File.ReadAllLines(j);
        var done = new HashSet<string>(
            lines.Where(l => l.StartsWith("done=", StringComparison.Ordinal)).Select(l => l.Substring(5)),
            StringComparer.OrdinalIgnoreCase);
        foreach (string rel in lines.Where(l => l.StartsWith("pair=", StringComparison.Ordinal)).Select(l => l.Substring(5)))
        {
            if (done.Contains(rel)) continue;
            string from = Resolve(files, rel), to = Resolve(root, rel);
            // The staging folder was cleaned under us. Abandon the journal rather than half-apply
            // it: the next start sees no install and offers a whole one, which is the right answer.
            if (!File.Exists(from)) { Wipe(stage); return; }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to));
            string old = to + ".old";
            if (File.Exists(old)) File.Delete(old);
            if (File.Exists(to)) File.Move(to, old);
            File.Move(from, to);
            if (File.Exists(old)) File.Delete(old);
            File.AppendAllText(j, "done=" + rel + "\n");
        }
        Wipe(stage);
    }

    private static void Wipe(string stage)
    {
        try { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
