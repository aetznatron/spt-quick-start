// SPT Quick Start
// Starts the SPT server, waits until it answers, then starts EscapeFromTarkov.exe
// with the profile that was last used in the SPT launcher
// (user\Launcher\LauncherSettings.json -> PreferredProfile.ProfileId).
//
// Options:  --profile <id>   force a specific profile
//           --launcher       start SPT.Launcher.exe instead of the game
//           --dry-run        start server and validate, but do not start the game
//           --keep-server    keep the server running after --dry-run

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

internal static class Program
{
    private const string BackendUrl = "https://127.0.0.1:6969";
    private const int StartTimeoutSeconds = 240;

    private static string Root;
    private static string Runtime;
    private static string ServerExe;
    private static string LauncherExe;
    private static string GameExe;
    private static string SettingsFile;
    private static string ProfilesDir;
    private static string LogFile;
    private static string PingUrl;

    private static bool dryRun;
    private static bool openLauncher;
    private static bool keepServer;
    private static string forcedProfile;

    private static int Main(string[] args)
    {
        Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        Runtime = Path.Combine(Root, "SPT_Runtime");
        ServerExe = Path.Combine(Runtime, "SPT.Server.exe");
        LauncherExe = Path.Combine(Runtime, "SPT.Launcher.exe");
        GameExe = Path.Combine(Root, "EscapeFromTarkov.exe");
        SettingsFile = Path.Combine(Runtime, @"user\Launcher\LauncherSettings.json");
        ProfilesDir = Path.Combine(Runtime, @"user\profiles");
        LogFile = Path.Combine(Root, "SPT-Start.log");
        PingUrl = BackendUrl + "/launcher/v2/ping";

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "--profile" && i + 1 < args.Length) { forcedProfile = args[++i]; }
            else if (a == "--launcher") { openLauncher = true; }
            else if (a == "--dry-run" || a == "--dryrun") { dryRun = true; }
            else if (a == "--keep-server") { keepServer = true; }
            else if (a == "--help" || a == "-h" || a == "/?")
            {
                Console.WriteLine("SPT Quick Start");
                Console.WriteLine("  --profile <id>   force a specific profile id");
                Console.WriteLine("  --launcher       start SPT.Launcher.exe instead of the game");
                Console.WriteLine("  --dry-run        start server and validate, do not start the game");
                Console.WriteLine("  --keep-server    keep the server running after --dry-run");
                return 0;
            }
        }

        try { Console.Title = "SPT Quick Start"; } catch { }

        // The SPT server only speaks TLS 1.2+ on its self-signed certificate.
        // .NET Framework negotiates old protocols by default and the handshake fails,
        // so TLS 1.2 must be forced and the certificate check bypassed (localhost only).
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }
        catch { }

        try
        {
            return Run();
        }
        catch (Exception ex)
        {
            return Fail("FATAL: " + ex.Message);
        }
    }

    private static int Run()
    {
        Say(new string('=', 62), ConsoleColor.DarkCyan);
        Say("  SPT Quick Start - " + Root, ConsoleColor.Cyan);
        Say(new string('=', 62), ConsoleColor.DarkCyan);

        if (!File.Exists(ServerExe)) return Fail("SPT.Server.exe not found: " + ServerExe);
        if (!openLauncher && !File.Exists(GameExe)) return Fail("EscapeFromTarkov.exe not found: " + GameExe);

        // ---------------------------------------------------------- 1. Server
        bool serverWasRunning = false;
        Say("[1/4] Checking server ...");

        if (ServerUp())
        {
            Say("  Server is already running and responding.", ConsoleColor.Green);
            serverWasRunning = true;
        }
        else if (PortOpen(6969))
        {
            Say("  Port 6969 is in use, waiting for the server to answer ...", ConsoleColor.Yellow);
        }
        else
        {
            Say("  Starting server ...");
            ProcessStartInfo info = new ProcessStartInfo(ServerExe);
            info.WorkingDirectory = Runtime;
            info.UseShellExecute = true;
            Process.Start(info);
        }

        // ------------------------------------------------- 2. Wait for server
        Say(string.Format("[2/4] Waiting for the server to finish loading (max {0}s) ...", StartTimeoutSeconds));
        Stopwatch sw = Stopwatch.StartNew();
        bool ready = false;
        int lastNote = 0;

        while (sw.Elapsed.TotalSeconds < StartTimeoutSeconds)
        {
            if (ServerUp()) { ready = true; break; }

            int elapsed = (int)sw.Elapsed.TotalSeconds;
            if (elapsed - lastNote >= 15)
            {
                bool alive = Process.GetProcessesByName("SPT.Server").Length > 0;
                if (!alive && !serverWasRunning && elapsed > 20)
                    return Fail("The server process has exited - aborting. Check the server console/logs.");
                Say(string.Format("  ... not ready yet ({0}s)", elapsed), ConsoleColor.DarkGray);
                lastNote = elapsed;
            }
            Thread.Sleep(1000);
        }

        if (!ready) return Fail(string.Format("Server did not answer on {0} within {1}s", PingUrl, StartTimeoutSeconds));
        Say(string.Format("  Server ready after {0:0.0}s.", sw.Elapsed.TotalSeconds), ConsoleColor.Green);

        // ---------------------------------------------------------- 3. Profile
        Say("[3/4] Resolving profile ...");
        string profileId = ResolveProfile();
        if (profileId == null) return 1;
        string nick = ProfileNickname(profileId);
        if (!string.IsNullOrEmpty(nick))
            Say(string.Format("  Profile: {0} ({1})", nick, profileId), ConsoleColor.Green);
        else
            Say("  Profile: " + profileId, ConsoleColor.Green);

        // ----------------------------------------------------------- 4. Client
        Say("[4/4] Starting client ...");

        if (openLauncher)
        {
            if (dryRun)
            {
                Say("  [dry-run] would start: " + LauncherExe, ConsoleColor.DarkGray);
            }
            else
            {
                ProcessStartInfo li = new ProcessStartInfo(LauncherExe);
                li.WorkingDirectory = Runtime;
                li.UseShellExecute = true;
                Process.Start(li);
                Say("  SPT.Launcher started.", ConsoleColor.Green);
            }
        }
        else
        {
            if (!dryRun)
            {
                string[] exclude = ExcludeFromCleanup();
                CleanGameItem("BattlEye", exclude);
                CleanGameItem("Logs", exclude);
                CleanGameItem("ConsistencyInfo", exclude);
                CleanGameItem("EscapeFromTarkov_BE.exe", exclude);
                CleanGameItem("Uninstall.exe", exclude);
                CleanGameItem("UnityCrashHandler64.exe", exclude);
                CleanGameItem("WinPixEventRuntime.dll", exclude);
                CleanGameItem(@"EscapeFromTarkov_Data\Plugins\x86_64\hwecho.dll", new string[0]);
            }

            string clientConfig = "{'BackendUrl':'" + BackendUrl + "','Version':'live','MatchingVersion':'live'}";
            string arguments = "-force-gfx-jobs native -token=" + profileId + " -config=" + clientConfig;

            if (dryRun)
            {
                Say("  [dry-run] command line:", ConsoleColor.DarkGray);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  \"" + GameExe + "\" " + arguments);
                Console.ResetColor();
            }
            else if (Process.GetProcessesByName("EscapeFromTarkov").Length > 0)
            {
                Say("  EscapeFromTarkov is already running - not starting a second instance.", ConsoleColor.Yellow);
            }
            else
            {
                ProcessStartInfo gi = new ProcessStartInfo(GameExe, arguments);
                gi.WorkingDirectory = Root;
                gi.UseShellExecute = false;
                Process.Start(gi);
                Say("  Client started. Have fun!", ConsoleColor.Green);
            }
        }

        if (dryRun && !keepServer && !serverWasRunning)
        {
            Say("[dry-run] Stopping the test server ...", ConsoleColor.DarkGray);
            foreach (Process p in Process.GetProcessesByName("SPT.Server"))
            {
                try { p.Kill(); } catch { }
            }
        }

        Say("Done.", ConsoleColor.DarkCyan);
        return 0;
    }

    // ------------------------------------------------------------- logging
    private static void Say(string text)
    {
        Say(text, ConsoleColor.Gray);
    }

    private static void Say(string text, ConsoleColor color)
    {
        string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text;
        Console.ForegroundColor = color;
        Console.WriteLine(line);
        Console.ResetColor();
        try { File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8); } catch { }
    }

    private static int Fail(string text)
    {
        Say(text, ConsoleColor.Red);
        Say("Aborted. See SPT-Start.log for details.", ConsoleColor.Red);
        if (!dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to close ...");
            try { Console.ReadKey(true); } catch { }
        }
        return 1;
    }

    // ---------------------------------------------------------------- server
    private static bool ServerUp()
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(PingUrl);
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.Proxy = null;
            request.ServerCertificateValidationCallback = delegate { return true; };
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                return response.StatusCode == HttpStatusCode.OK;
            }
        }
        catch { return false; }
    }

    private static bool PortOpen(int port)
    {
        TcpClient client = new TcpClient();
        try
        {
            IAsyncResult ar = client.BeginConnect("127.0.0.1", port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(800)) return false;
            client.EndConnect(ar);
            return true;
        }
        catch { return false; }
        finally { try { client.Close(); } catch { } }
    }

    // --------------------------------------------------------------- profile
    private static string ResolveProfile()
    {
        if (!string.IsNullOrEmpty(forcedProfile))
        {
            if (!File.Exists(Path.Combine(ProfilesDir, forcedProfile + ".json")))
            {
                Fail("Profile '" + forcedProfile + "' does not exist in " + ProfilesDir);
                return null;
            }
            return forcedProfile;
        }

        object settings = ReadJson(SettingsFile);
        object preferred = Get(settings, "PreferredProfile", "ProfileId");
        if (preferred is string)
        {
            string id = (string)preferred;
            if (File.Exists(Path.Combine(ProfilesDir, id + ".json")))
            {
                Say("Last used profile from LauncherSettings: " + id, ConsoleColor.DarkGray);
                return id;
            }
            Say("PreferredProfile '" + id + "' has no profile file, looking for an alternative ...", ConsoleColor.Yellow);
        }

        string[] files = Directory.Exists(ProfilesDir)
            ? Directory.GetFiles(ProfilesDir, "*.json")
            : new string[0];

        if (files.Length == 1)
        {
            string only = Path.GetFileNameWithoutExtension(files[0]);
            Say("Only one profile present, using: " + only, ConsoleColor.DarkGray);
            return only;
        }
        if (files.Length == 0)
        {
            Fail("No profile found in " + ProfilesDir);
            return null;
        }

        Say("Multiple profiles, none marked as last used:", ConsoleColor.Yellow);
        foreach (string file in files)
        {
            string nick = ProfileNickname(Path.GetFileNameWithoutExtension(file));
            Console.WriteLine(string.Format("   {0}  {1}", Path.GetFileNameWithoutExtension(file), nick));
        }
        Fail("Start the game once through SPT.Launcher (this sets PreferredProfile) or pass --profile <id>.");
        return null;
    }

    private static string ProfileNickname(string profileId)
    {
        object profile = ReadJson(Path.Combine(ProfilesDir, profileId + ".json"));
        object nick = Get(profile, "characters", "pmc", "Info", "Nickname");
        if (nick is string) return (string)nick;
        nick = Get(profile, "pmc", "Info", "Nickname");
        if (nick is string) return (string)nick;
        return "";
    }

    private static string[] ExcludeFromCleanup()
    {
        object settings = ReadJson(SettingsFile);
        object list = Get(settings, "ExcludeFromCleanup");
        List<string> result = new List<string>();
        List<object> items = list as List<object>;
        if (items != null)
        {
            foreach (object item in items)
            {
                string s = item as string;
                if (!string.IsNullOrEmpty(s)) result.Add(s);
            }
        }
        return result.ToArray();
    }

    private static void CleanGameItem(string relativePath, string[] exclude)
    {
        string leaf = Path.GetFileName(relativePath);
        foreach (string e in exclude)
        {
            if (string.Equals(e, leaf, StringComparison.OrdinalIgnoreCase)) return;
        }

        string full = Path.Combine(Root, relativePath);
        try
        {
            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
                Say("  removed: " + relativePath, ConsoleColor.DarkGray);
            }
            else if (File.Exists(full))
            {
                File.SetAttributes(full, FileAttributes.Normal);
                File.Delete(full);
                Say("  removed: " + relativePath, ConsoleColor.DarkGray);
            }
        }
        catch { Say("  could not remove: " + relativePath, ConsoleColor.DarkYellow); }
    }

    // ------------------------------------------------------------ mini JSON
    private static object ReadJson(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            int index = 0;
            return ParseValue(File.ReadAllText(path, Encoding.UTF8), ref index);
        }
        catch { return null; }
    }

    private static object Get(object node, params string[] keys)
    {
        object current = node;
        foreach (string key in keys)
        {
            Dictionary<string, object> dict = current as Dictionary<string, object>;
            if (dict == null || !dict.ContainsKey(key)) return null;
            current = dict[key];
        }
        return current;
    }

    private static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static object ParseValue(string s, ref int i)
    {
        SkipWhitespace(s, ref i);
        if (i >= s.Length) throw new FormatException("unexpected end of JSON");

        char c = s[i];
        if (c == '{') return ParseObject(s, ref i);
        if (c == '[') return ParseArray(s, ref i);
        if (c == '"') return ParseString(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

        int start = i;
        while (i < s.Length && "+-.0123456789eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, object> ParseObject(string s, ref int i)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        i++; // {
        SkipWhitespace(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return result; }

        while (true)
        {
            SkipWhitespace(s, ref i);
            string key = ParseString(s, ref i);
            SkipWhitespace(s, ref i);
            if (s[i] != ':') throw new FormatException("expected ':' at " + i);
            i++;
            result[key] = ParseValue(s, ref i);
            SkipWhitespace(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == '}') { i++; return result; }
            throw new FormatException("expected ',' or '}' at " + i);
        }
    }

    private static List<object> ParseArray(string s, ref int i)
    {
        List<object> result = new List<object>();
        i++; // [
        SkipWhitespace(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return result; }

        while (true)
        {
            result.Add(ParseValue(s, ref i));
            SkipWhitespace(s, ref i);
            if (s[i] == ',') { i++; continue; }
            if (s[i] == ']') { i++; return result; }
            throw new FormatException("expected ',' or ']' at " + i);
        }
    }

    private static string ParseString(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("expected string at " + i);
        i++;
        StringBuilder sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }

            char escape = s[i++];
            switch (escape)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default: sb.Append(escape); break;
            }
        }
        throw new FormatException("unterminated string");
    }
}
