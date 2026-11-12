using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualHeritage
{
    [Serializable] public class GuideLanguage { public string code, name, voice; }
    [Serializable] public class GuideEntry
    {
        public string key, en, hi, kn;
        public string Text(string code) => code == "hi" ? hi : code == "kn" ? kn : en;
    }
    [Serializable] public class TempleGuide { public string id; public GuideEntry[] entries; }
    [Serializable] public class GuideData { public GuideLanguage[] languages; public TempleGuide[] temples; }

    // Multilingual audio guide. Narrates the place you walk into (courtyard, steps, platform, hall, sanctum...) and any
    // architectural element you select, in English, Hindi or Kannada. Clips are bundled (Resources/Guide) so it works offline.
    public sealed class HeritageGuide : MonoBehaviour
    {
        enum Kind { Welcome, Zone, Element }

        public bool Enabled { get; private set; } = true;
        public int LanguageIndex { get; private set; }
        public string LanguageName => data.languages[LanguageIndex].name;
        public string LanguageCode => data.languages[LanguageIndex].code;
        public string Caption { get; private set; } = "";
        public int PlayCount { get; private set; }
        public string CurrentKey { get; private set; } = "";
        public event Action CaptionChanged;

        // Headless test runs have no audio device; --silent-guide keeps all the logic and captions but plays no sound.
        static readonly bool Silent = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--silent-guide");
        HeritageApp app; GuideData data; AudioSource voice; AudioClip clip;
        readonly HashSet<string> heard = new HashSet<string>();
        Kind kind; string playingKey, playingZone, pendingZone, lastPlayed;
        string zone; float zoneSince, welcomeAt, hideCaptionAt;

        public void Initialize(HeritageApp owner)
        {
            app = owner;
            var file = Resources.Load<TextAsset>("guide");
            if (!file) throw new InvalidOperationException("Audio guide text is missing.");
            data = JsonUtility.FromJson<GuideData>(file.text);
            voice = gameObject.AddComponent<AudioSource>(); voice.spatialBlend = 0; voice.volume = 1; voice.playOnAwake = false;
            try
            {
                LanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt("guide.language", 0), 0, data.languages.Length - 1);
                Enabled = PlayerPrefs.GetInt("guide.enabled", 1) == 1;
            }
            catch (Exception) { /* storage can be unavailable; defaults apply */ }
        }

        public void SetEnabled(bool on)
        {
            Enabled = on; Save();
            if (!on) StopVoice(); else if (app.TempleIndex >= 0) { heard.Clear(); zone = null; welcomeAt = Time.time + .5f; }
            CaptionChanged?.Invoke();
        }

        public void CycleLanguage() { SetLanguage((LanguageIndex + 1) % data.languages.Length); }

        public void SetLanguage(int index)
        {
            LanguageIndex = Mathf.Clamp(index, 0, data.languages.Length - 1); Save();
            if (Enabled && voice.isPlaying && !string.IsNullOrEmpty(playingKey)) Play(playingKey, kind, playingZone);
            CaptionChanged?.Invoke();
        }

        void Save() { try { PlayerPrefs.SetInt("guide.language", LanguageIndex); PlayerPrefs.SetInt("guide.enabled", Enabled ? 1 : 0); PlayerPrefs.Save(); } catch (Exception) { } }

        // Called when a temple (or the gallery, index -1) has just been built.
        public void OnLocation(int index)
        {
            StopVoice(); heard.Clear(); zone = null; pendingZone = null;
            welcomeAt = index >= 0 && Enabled ? Time.time + 1.4f : 0;
        }

        public void PlayElement(int index) { if (Enabled && app.TempleIndex >= 0) Play("el" + index, Kind.Element, null); }

        // Replays the narration for wherever the visitor is standing (or the welcome if nowhere in particular).
        public void Replay()
        {
            if (!Enabled || app.TempleIndex < 0) return;
            string key = !string.IsNullOrEmpty(zone) ? zone : (string.IsNullOrEmpty(lastPlayed) ? "welcome" : lastPlayed);
            Play(key, key == "welcome" ? Kind.Welcome : key.StartsWith("el") ? Kind.Element : Kind.Zone, key);
        }

        void Update()
        {
            if (playingKey != null && voice.clip != null && !voice.isPlaying) FinishIfDone();
            if (hideCaptionAt > 0 && Time.time > hideCaptionAt)
            {
                hideCaptionAt = 0; Caption = ""; CaptionChanged?.Invoke();
                if (voice.clip == null && playingKey != null) { if (kind != Kind.Element) heard.Add(playingKey); playingKey = null; playingZone = null; }
            }
            if (!Enabled || app.TempleIndex < 0 || app.Transitioning) return;
            if (welcomeAt > 0 && Time.time >= welcomeAt) { welcomeAt = 0; Play("welcome", Kind.Welcome, null); return; }

            string now = ZoneAt(app.TempleIndex, app.rig.GuidePosition);
            if (now != zone) { zone = now; zoneSince = Time.time; }
            else if (now != null && Time.time - zoneSince > .8f && !heard.Contains(now) && playingZone != now) Request(now);

            if (!Busy && pendingZone != null)
            {
                string want = pendingZone; pendingZone = null;
                if (zone == want && !heard.Contains(want)) Play(want, Kind.Zone, want);
            }
        }

        bool Busy => voice.isPlaying || playingKey != null;

        void Request(string key)
        {
            if (!Busy) { Play(key, Kind.Zone, key); return; }
            // A newly entered zone replaces a zone talk, but never cuts off the welcome or a selected element.
            if (kind == Kind.Zone) Play(key, Kind.Zone, key); else pendingZone = key;
        }

        void Play(string key, Kind what, string zoneKey)
        {
            var temple = Array.Find(data.temples, t => t.id == app.CurrentTemple.id);
            var entry = temple == null ? null : Array.Find(temple.entries, e => e.key == key);
            if (entry == null) return;
            StopVoice(false);
            var next = Silent ? null : Resources.Load<AudioClip>("Guide/" + LanguageCode + "/" + temple.id + "_" + key);
            clip = next; kind = what; playingKey = key; playingZone = zoneKey; lastPlayed = key; CurrentKey = key;
            Caption = entry.en; hideCaptionAt = 0; PlayCount++;
            if (clip) { voice.clip = clip; voice.Play(); } else { voice.clip = null; hideCaptionAt = Time.time + (Silent ? 2 : 9); }
            CaptionChanged?.Invoke();
        }

        void FinishIfDone()
        {
            if (kind != Kind.Element) heard.Add(playingKey);
            hideCaptionAt = Time.time + 2.5f; playingKey = null; playingZone = null;
            clip = null; voice.clip = null;   // clips are small and stay cached by Resources
        }

        void StopVoice(bool clearCaption = true)
        {
            if (voice) { if (voice.isPlaying) voice.Stop(); voice.clip = null; }
            clip = null;
            playingKey = null; playingZone = null; hideCaptionAt = 0;
            if (clearCaption && Caption.Length > 0) { Caption = ""; CaptionChanged?.Invoke(); }
        }

        // Which named part of the temple the visitor is in (feet position, world coordinates), or null.
        public static string ZoneAt(int temple, Vector3 p)
        {
            if (temple == 2)
            {
                if (p.z > 5.5f && Mathf.Abs(p.x) < 5f) return "gopuram";
                if (Mathf.Abs(p.x) > 4f && p.z > -1f && p.z < 10f) return "pavilion";
                return p.z > -3f ? "court" : null;
            }
            float[] sanctums = temple == 1 ? new[] { -3.9f, 3.9f } : new[] { 0f };
            bool raised = p.y > .6f;
            foreach (float sx in sanctums) if (raised && Mathf.Abs(p.x - sx) < 1.3f && p.z > 5.8f && p.z < 7.8f) return "sanctum";
            if (raised && p.z > 1.1f && p.z < 5.7f && Mathf.Abs(p.x) < (temple == 1 ? 6.6f : 2.8f)) return "hall";
            foreach (float sx in sanctums) if (Mathf.Abs(p.x - sx) < 1.7f && p.z < -.1f && p.z > -2.8f) return "steps";
            if (raised && p.z > -1f) return "platform";
            return p.z < -.1f && p.z > -9f ? "courtyard" : null;
        }
    }
}
