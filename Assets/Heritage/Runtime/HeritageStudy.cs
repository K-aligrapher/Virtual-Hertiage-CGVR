using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace VirtualHeritage
{
    [Serializable] public class StudyQuestion { public string id, text; public string[] options; public int correct; }
    [Serializable] public class SsqItem { public string name; public int n, o, d; }
    [Serializable] public class LikertItem { public string id, text; public bool guideOnly; }
    [Serializable] public class StudyData { public StudyQuestion[] questions; public SsqItem[] ssq; public LikertItem[] likert; }

    public struct StudyOption { public string label; public Action action; public bool primary; }

    // One screen of the study wizard, drawn by HeritageUI in the same panel on desktop, phone and headset.
    public sealed class StudyPage
    {
        public string eyebrow = "", title = "", body = "";
        public readonly List<StudyOption> options = new List<StudyOption>();
        public bool keypad; public string code = ""; public Action<string> onCode;
        public float optionHeight = 52;
        public void Add(string label, Action action, bool primary = false) { options.Add(new StudyOption { label = label, action = action, primary = primary }); }
    }

    // Measures learning gain (pre/post knowledge test) and comfort (SSQ before/after, FMS probes, 7-point ratings)
    // and writes one CSV per participant. Everything is anonymous: only a participant code is stored.
    public sealed class HeritageStudy : MonoBehaviour
    {
        enum Phase { Idle, Setup, PreSsq, PreTest, Explore, PostSsq, PostTest, Rating, Done }
        struct Answer { public string id; public int chosen; public bool correct; public float seconds; }

        public bool Running => phase != Phase.Idle;
        public bool Exploring => phase == Phase.Explore;
        public float Elapsed { get; private set; }
        public float MinSeconds { get; private set; } = 420;
        public float MaxSeconds { get; private set; } = 900;
        public bool CanFinish => Elapsed >= MinSeconds;
        public string LastSavedPath { get; private set; } = "";

        HeritageApp app; StudyData data; Phase phase;
        float probeEvery = 150, nextProbe, questionShownAt;
        bool probeShowing, restoreGuide; int restoreLanguage;
        string participant = "000", platform = "", guideLanguage = "";
        bool guideOn; int vrExperience, priorKnowledge;
        int[] ssqPre, ssqPost; readonly List<Answer> pre = new List<Answer>(), post = new List<Answer>();
        readonly List<string> fms = new List<string>(); readonly Dictionary<string,int> ratings = new Dictionary<string,int>();
        readonly float[] secondsAt = new float[4]; DateTime startedAt; int lastTemple = -2;
        float startMetres; int startGuidePlays, startTeleports; readonly HashSet<int> visited = new HashSet<int>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HeritageDownloadText(string filename, string content);
#endif

        public void Initialize(HeritageApp owner)
        {
            app = owner;
            var file = Resources.Load<TextAsset>("study");
            if (!file) throw new InvalidOperationException("Study content is missing.");
            data = JsonUtility.FromJson<StudyData>(file.text);
            if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "--study-fast")) { MinSeconds = 12; MaxSeconds = 60; probeEvery = 6; }
        }

        // ---------- Flow ----------

        public void Begin()
        {
            if (Running) return;
            phase = Phase.Setup; pre.Clear(); post.Clear(); fms.Clear(); ratings.Clear(); visited.Clear();
            Array.Clear(secondsAt, 0, secondsAt.Length); Elapsed = 0; lastTemple = -2;
            ssqPre = new int[data.ssq.Length]; ssqPost = new int[data.ssq.Length];
            restoreGuide = app.guide.Enabled; restoreLanguage = app.guide.LanguageIndex;
            app.guide.SetEnabled(false);
            if (app.TempleIndex >= 0) app.OpenHub();
            var p = new StudyPage { eyebrow = "STUDENT STUDY", title = "Learn and tell us how it felt" };
            p.body = "You will take a short quiz, explore three temples for about " + Mathf.RoundToInt(MinSeconds / 60f) + " to " + Mathf.RoundToInt(MaxSeconds / 60f) +
                " minutes, answer comfort questions, then repeat the quiz. About 20 minutes in total.\n\nNo personal details are collected, only a participant code. You may stop at any time. If you feel unwell, take the headset off and tell the supervisor.";
            p.Add("I agree, start", AskCode, true); p.Add("Not now", Cancel);
            Show(p);
        }

        void Cancel() { EndSession(false); }

        void AskCode()
        {
            var p = new StudyPage { eyebrow = "SETUP  /  1 OF 4", title = "Participant code", body = "Enter the 3-digit code given by your supervisor.", keypad = true };
            p.onCode = code => { participant = code; AskGuide(); };
            Show(p);
        }

        void AskGuide()
        {
            var p = new StudyPage { eyebrow = "SETUP  /  2 OF 4", title = "Audio guide for this session", body = "Your supervisor tells you which to choose." };
            p.Add("With audio guide: English", () => SetGuide(true, 0)); p.Add("With audio guide: Hindi", () => SetGuide(true, 1)); p.Add("With audio guide: Kannada", () => SetGuide(true, 2));
            p.Add("Without audio guide", () => SetGuide(false, 0));
            Show(p);
        }

        void SetGuide(bool on, int language)
        {
            guideOn = on; app.guide.SetLanguage(language); guideLanguage = on ? app.guide.LanguageCode : "none"; app.guide.SetEnabled(false);
            Scale("SETUP  /  3 OF 4", "How much experience do you have with VR?", "1 = none, 5 = very experienced.", 1, 5, v => { vrExperience = v; Scale("SETUP  /  4 OF 4", "How familiar are you with Hoysala and Vijayanagara architecture?", "1 = not at all, 5 = very familiar.", 1, 5, w => { priorKnowledge = w; Phase_(Phase.PreSsq); SsqPage(0, true); }); });
        }

        void Scale(string eyebrow, string title, string body, int from, int to, Action<int> done)
        {
            var p = new StudyPage { eyebrow = eyebrow, title = title, body = body, optionHeight = 44 };
            for (int v = from; v <= to; v++) { int value = v; p.Add(value.ToString(), () => done(value)); }
            Show(p);
        }

        void Phase_(Phase next) { phase = next; }

        void SsqPage(int i, bool before)
        {
            if (i >= data.ssq.Length) { if (before) StartPreTest(); else StartPostTest(); return; }
            var p = new StudyPage { eyebrow = (before ? "COMFORT BEFORE" : "COMFORT AFTER") + "  /  " + (i + 1) + " OF " + data.ssq.Length, title = data.ssq[i].name, body = "Right now, how much does this bother you?", optionHeight = 50 };
            string[] labels = { "None", "Slight", "Moderate", "Severe" };
            for (int v = 0; v < 4; v++) { int value = v; p.Add(labels[v], () => { (before ? ssqPre : ssqPost)[i] = value; SsqPage(i + 1, before); }); }
            Show(p);
        }

        // ----- Knowledge test (order of questions and answers is shuffled per participant and per test) -----

        List<int> order; int[][] optionOrder;

        void StartTest(bool before)
        {
            var rng = new System.Random((int.TryParse(participant, out int n) ? n : 0) * 7919 + (before ? 1 : 2));
            order = new List<int>(); for (int i = 0; i < data.questions.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            optionOrder = new int[data.questions.Length][];
            for (int q = 0; q < optionOrder.Length; q++)
            {
                var perm = new List<int>(); for (int k = 0; k < data.questions[q].options.Length; k++) perm.Add(k);
                for (int i = perm.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
                optionOrder[q] = perm.ToArray();
            }
            TestPage(0, before);
        }

        void StartPreTest() { phase = Phase.PreTest; StartTest(true); }
        void StartPostTest() { phase = Phase.PostTest; StartTest(false); }

        void TestPage(int i, bool before)
        {
            if (i >= order.Count) { if (before) StartExplore(); else StartRatings(); return; }
            var q = data.questions[order[i]];
            var p = new StudyPage { eyebrow = (before ? "KNOWLEDGE CHECK BEFORE" : "KNOWLEDGE CHECK AFTER") + "  /  " + (i + 1) + " OF " + order.Count, title = q.text, optionHeight = 50 };
            foreach (int k in optionOrder[order[i]]) { int choice = k; p.Add(q.options[k], () => Record(before, q, choice, i)); }
            p.Add("I do not know", () => Record(before, q, -1, i));
            questionShownAt = Time.unscaledTime; Show(p);
        }

        void Record(bool before, StudyQuestion q, int choice, int index)
        {
            (before ? pre : post).Add(new Answer { id = q.id, chosen = choice, correct = choice == q.correct, seconds = Time.unscaledTime - questionShownAt });
            TestPage(index + 1, before);
        }

        // ----- Exploration -----

        void StartExplore()
        {
            phase = Phase.Explore; Elapsed = 0; nextProbe = probeEvery; startedAt = DateTime.UtcNow;
            startMetres = app.rig.WalkedMetres; startGuidePlays = app.guide.PlayCount; startTeleports = app.rig.TeleportCount;
            app.guide.SetEnabled(guideOn);
            app.OpenHub(); RestorePanel();
        }

        void Update()
        {
            if (phase != Phase.Explore) return;
            Elapsed += Time.unscaledDeltaTime;
            int temple = app.TempleIndex;
            if (temple >= 0 && !app.Transitioning) { visited.Add(temple); secondsAt[temple + 1] += Time.unscaledDeltaTime; }
            app.ui.UpdateStudyHud(Elapsed, MinSeconds, CanFinish);
            if (!probeShowing && Elapsed >= nextProbe) ShowProbe();
            if (!probeShowing && Elapsed >= MaxSeconds) Finish();
        }

        void ShowProbe()
        {
            probeShowing = true; nextProbe = Elapsed + probeEvery;
            var p = new StudyPage { eyebrow = "QUICK COMFORT CHECK", title = "How do you feel right now?", body = "0 means no sickness at all, 20 means severe sickness. If you feel unwell, finish the study at any time.", optionHeight = 46 };
            string[] labels = { "0  No sickness", "5  Slightly unwell", "10  Moderately unwell", "15  Quite unwell", "20  Severe" };
            for (int k = 0; k < 5; k++) { int score = k * 5; p.Add(labels[k], () => { fms.Add(Mathf.RoundToInt(Elapsed) + ":" + score); probeShowing = false; RestorePanel(); }); }
            Show(p);
        }

        public void Finish()
        {
            if (phase != Phase.Explore) return;
            probeShowing = false; phase = Phase.PostSsq; app.guide.SetEnabled(false);
            if (app.TempleIndex >= 0) app.OpenHub();
            SsqPage(0, false);
        }

        void StartRatings() { phase = Phase.Rating; RatingPage(0); }

        void RatingPage(int i)
        {
            while (i < data.likert.Length && data.likert[i].guideOnly && !guideOn) i++;
            if (i >= data.likert.Length) { Complete(); return; }
            var item = data.likert[i];
            var p = new StudyPage { eyebrow = "YOUR EXPERIENCE  /  " + (i + 1) + " OF " + data.likert.Length, title = item.text, body = "1 = strongly disagree, 7 = strongly agree.", optionHeight = 42 };
            for (int v = 1; v <= 7; v++) { int value = v; p.Add(value.ToString(), () => { ratings[item.id] = value; RatingPage(i + 1); }); }
            Show(p);
        }

        // ---------- Results ----------

        static int Score(List<Answer> a) { int n = 0; foreach (var x in a) if (x.correct) n++; return n; }

        static float[] Ssq(SsqItem[] items, int[] s)
        {
            float n = 0, o = 0, d = 0;
            for (int i = 0; i < items.Length; i++) { n += items[i].n * s[i]; o += items[i].o * s[i]; d += items[i].d * s[i]; }
            return new[] { n * 9.54f, o * 7.58f, d * 13.92f, (n + o + d) * 3.74f };
        }

        bool withdrawn; Phase exitPhase;

        string Export(out int total, out int a, out int b, out float gain, out float[] s0, out float[] s1)
        {
            total = data.questions.Length; a = Score(pre); b = Score(post);
            gain = a < total ? (b - a) / (float)(total - a) : float.NaN;
            s0 = Ssq(data.ssq, ssqPre); s1 = Ssq(data.ssq, ssqPost);
            return SaveCsv(total, a, b, gain, s0, s1);
        }

        void Complete()
        {
            phase = Phase.Done; withdrawn = false; exitPhase = Phase.Done;
            string path = Export(out int total, out int a, out int b, out float gain, out float[] s0, out float[] s1);
            var p = new StudyPage { eyebrow = "THANK YOU", title = "Study complete" };
            p.body = "Knowledge: " + a + " / " + total + " before, " + b + " / " + total + " after.\nLearning gain: " + (float.IsNaN(gain) ? "n/a" : (gain * 100).ToString("0") + "% of the possible improvement") +
                ".\nComfort (SSQ total): " + s0[3].ToString("0.0") + " before, " + s1[3].ToString("0.0") + " after.\n\nYour results file: " + path;
            p.Add("Finish", () => EndSession(true), true);
            Show(p);
        }

        // A participant can stop at any time. What was collected so far is saved and marked as withdrawn.
        public void Abort()
        {
            if (!Running) return;
            if (phase >= Phase.PreTest && phase != Phase.Done) { withdrawn = true; exitPhase = phase; Export(out _, out _, out _, out _, out _, out _); }
            EndSession(false);
        }

        void EndSession(bool completed)
        {
            phase = Phase.Idle; probeShowing = false;
            app.guide.SetLanguage(restoreLanguage); app.guide.SetEnabled(restoreGuide);
            app.ui.ShowHub();
        }

        static string Csv(string v) { return v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v; }

        string SaveCsv(int total, int a, int b, float gain, float[] s0, float[] s1)
        {
            var cols = new List<string>(); var vals = new List<string>();
            void Add(string c, object v) { cols.Add(c); vals.Add(v is float f ? (float.IsNaN(f) ? "" : f.ToString("0.###", CultureInfo.InvariantCulture)) : Convert.ToString(v, CultureInfo.InvariantCulture)); }
            platform = Describe();
            Add("completed", withdrawn ? "no" : "yes"); Add("exit_phase", withdrawn ? exitPhase.ToString() : "Done");
            Add("participant", participant); Add("started_utc", startedAt.ToString("o")); Add("platform", platform); Add("guide", guideOn ? "on" : "off"); Add("guide_language", guideLanguage);
            Add("app_version", Application.version); Add("vr_experience_1to5", vrExperience); Add("prior_knowledge_1to5", priorKnowledge);
            Add("explore_seconds", Elapsed); Add("seconds_gallery", secondsAt[0]); Add("seconds_belur", secondsAt[1]); Add("seconds_halebidu", secondsAt[2]); Add("seconds_hampi", secondsAt[3]);
            Add("temples_visited", visited.Count); Add("walked_metres", app.rig.WalkedMetres - startMetres); Add("teleports", app.rig.TeleportCount - startTeleports); Add("guide_plays", app.guide.PlayCount - startGuidePlays);
            Add("pre_correct", a); Add("post_correct", b); Add("questions", total); Add("pre_percent", a * 100f / total); Add("post_percent", b * 100f / total);
            Add("gain_points", b - a); Add("normalized_gain", gain);
            string[] names = { "ssq_nausea", "ssq_oculomotor", "ssq_disorientation", "ssq_total" };
            for (int i = 0; i < 4; i++) { Add(names[i] + "_pre", s0[i]); Add(names[i] + "_post", s1[i]); }
            Add("fms_probes_seconds_score", string.Join(";", fms)); Add("fms_max", MaxFms());
            foreach (var l in data.likert) Add("rating_" + l.id, ratings.TryGetValue(l.id, out int r) ? (object)r : "");
            for (int i = 0; i < data.questions.Length; i++) { var id = data.questions[i].id; Add("pre_" + id, Find(pre, id)); Add("post_" + id, Find(post, id)); }
            for (int i = 0; i < data.ssq.Length; i++) { Add("ssq_pre_" + (i + 1), ssqPre[i]); Add("ssq_post_" + (i + 1), ssqPost[i]); }
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols.ConvertAll(Csv))); sb.AppendLine(string.Join(",", vals.ConvertAll(Csv)));
            string name = "heritage_study_P" + participant + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".csv";
#if UNITY_WEBGL && !UNITY_EDITOR
            HeritageDownloadText(name, sb.ToString()); LastSavedPath = "downloaded as " + name; return LastSavedPath;
#else
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "study"); Directory.CreateDirectory(folder);
                LastSavedPath = Path.Combine(folder, name); File.WriteAllText(LastSavedPath, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception e) { Debug.LogWarning("HERITAGE: could not save study results: " + e.Message); LastSavedPath = "not saved (" + e.Message + ")"; }
            return LastSavedPath;
#endif
        }

        static string Find(List<Answer> list, string id)
        {
            foreach (var x in list) if (x.id == id) return (x.correct ? "1" : "0") + "|" + x.chosen + "|" + x.seconds.ToString("0.0", CultureInfo.InvariantCulture);
            return "";
        }

        int MaxFms() { int m = 0; foreach (var e in fms) m = Mathf.Max(m, int.Parse(e.Substring(e.IndexOf(':') + 1))); return m; }

        string Describe()
        {
            if (app.rig.IsVR) return Application.platform == RuntimePlatform.Android ? "Quest standalone VR" : "PC VR headset";
            if (Application.platform == RuntimePlatform.WebGLPlayer) return Application.isMobilePlatform || app.rig.TouchActive ? "Mobile browser" : "Desktop browser";
            return Application.platform == RuntimePlatform.Android ? "Android touch" : "Desktop app";
        }

        // Hooks used by the interface.
        public StudyPage CurrentPage { get; private set; }
        public bool ProbeShowing => probeShowing;
        void Show(StudyPage page) { CurrentPage = page; app.ui.ShowStudyPage(page); }
        public void RestorePanel() { app.ui.RestoreAfterStudy(); }
    }
}
