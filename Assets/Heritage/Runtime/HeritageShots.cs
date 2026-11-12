using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace VirtualHeritage
{
    // Developer-only: renders the main camera from scripted viewpoints to PNG files (--heritage-shots).
    public sealed class HeritageShots : MonoBehaviour
    {
        struct Shot { public string name; public int temple; public bool then; public Vector3 feet, look; public float height; }
        HeritageApp app;
        string output;

        IEnumerator Start()
        {
            app = GetComponent<HeritageApp>();
            output = Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/shots"));
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--shots-out") output = args[i+1];
            Directory.CreateDirectory(output);
            string only = null;
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--shots-only") only = args[i+1];
            yield return new WaitForSeconds(1f);
            foreach (var shot in Shots())
            {
                if (only != null && !shot.name.StartsWith(only)) continue;
                if (app.TempleIndex != shot.temple) { app.OpenTemple(shot.temple); yield return new WaitForSeconds(1.2f); }
                app.SetThen(shot.then);
                yield return new WaitForSeconds(shot.then ? 1.2f : .2f);
                app.rig.Place(shot.feet,shot.look,shot.height);
                yield return null; yield return null;
                yield return Capture(shot.name);
            }
            if (Array.Exists(Environment.GetCommandLineArgs(),a => a == "--guide-test")) yield return GuideTest();
            if (Array.Exists(Environment.GetCommandLineArgs(),a => a == "--study-test")) yield return StudyTest();
            if (Array.Exists(Environment.GetCommandLineArgs(),a => a == "--walk-check"))
                foreach (int t in new[] { 0, 1 })
                {
                    app.OpenTemple(t); yield return new WaitForSeconds(1.5f);
                    Physics.SyncTransforms();
                    float sx = t == 1 ? -3.9f : 0f;
                    Debug.Log("HERITAGE_WALK temple " + t + " from spawn reaches front steps/hall/sanctum: " + string.Join(", ", Reach(new Vector3(10,0,-17), new[] { new Vector3(sx,0,-3), new Vector3(sx,0,2), new Vector3(sx,0,6) })));
                }
            Debug.Log("HERITAGE_SHOTS done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        // Headless runs cannot use ScreenCapture, so the overlay UI is drawn through the camera for the shot.
        IEnumerator Screen(string name)
        {
            var canvas = app.ui.canvas; var mode = canvas.renderMode; var camera = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = app.rig.view; canvas.planeDistance = .6f;
            yield return null; yield return null;
            yield return Capture(name);
            canvas.renderMode = mode; canvas.worldCamera = camera;
        }

        // Walks through each guide zone and checks the right narration is chosen, in English then Hindi.
        IEnumerator GuideTest()
        {
            Debug.Log("HERITAGE_GUIDE start"); app.OpenTemple(0); yield return new WaitForSeconds(3.5f);
            Debug.Log("HERITAGE_GUIDE opened"); app.guide.SetEnabled(true); Debug.Log("HERITAGE_GUIDE enabled"); app.guide.SetLanguage(0); Debug.Log("HERITAGE_GUIDE language");
            var log = new System.Text.StringBuilder("HERITAGE_GUIDE welcome=" + app.guide.CurrentKey + " plays=" + app.guide.PlayCount);
            var stops = new[] { ("courtyard",new Vector3(0,0,-5)), ("steps",new Vector3(0,0,-1.6f)), ("platform",new Vector3(0,.72f,0)), ("hall",new Vector3(0,.82f,3)), ("sanctum",new Vector3(0,.82f,6.2f)) };
            foreach (var (expect,pos) in stops)
            {
                Debug.Log("HERITAGE_GUIDE stop " + expect); app.rig.Place(pos,pos + Vector3.forward,1.7f); Debug.Log("HERITAGE_GUIDE placed " + expect); yield return new WaitForSeconds(2.6f);
                log.Append(" | " + expect + "->" + app.guide.CurrentKey);
            }
            app.guide.SetLanguage(1); app.SelectElement(1); yield return new WaitForSeconds(.5f);
            log.Append(" | element(hi)->" + app.guide.CurrentKey + " lang=" + app.guide.LanguageName);
            yield return Screen("62-guide-caption-hindi");
            app.ClearSelection(); app.guide.SetLanguage(0);
            log.Append(" | totalPlays=" + app.guide.PlayCount);
            Debug.Log(log.ToString());
        }

        IEnumerator StudyTest()
        {
            var study = app.Study; app.OpenHub(); yield return new WaitForSeconds(1.2f);
            study.Begin(); yield return null; yield return Screen("60-study-intro");
            bool shotQuestion = false, shotExplore = false; int guard = 0; var clicked = new System.Text.StringBuilder();
            while (study.Running && guard++ < 600)
            {
                var page = study.CurrentPage;
                if (study.Exploring && !study.ProbeShowing)
                {
                    if (!shotExplore) { shotExplore = true; app.OpenTemple(0); yield return new WaitForSeconds(2f); app.rig.Place(new Vector3(0,.82f,3),new Vector3(0,1.8f,6),1.7f); yield return new WaitForSeconds(3f); yield return Screen("61-study-explore"); }
                    if (study.CanFinish) study.Finish(); else yield return new WaitForSeconds(.4f);
                    continue;
                }
                if (page == null) { yield return null; continue; }
                if (page.keypad) { yield return Screen("63-study-code"); page.onCode("042"); }
                else if (page.options.Count > 0)
                {
                    if (!shotQuestion && page.eyebrow.StartsWith("KNOWLEDGE")) { shotQuestion = true; yield return Screen("64-study-question"); }
                    page.options[page.eyebrow == "STUDENT STUDY" ? 0 : guard % page.options.Count].action();
                }
                yield return null;
            }
            string folder = Path.Combine(Application.persistentDataPath,"study");
            var files = Directory.Exists(folder) ? Directory.GetFiles(folder,"*.csv") : new string[0];
            string last = files.Length > 0 ? files[files.Length - 1] : "";
            Debug.Log("HERITAGE_STUDY running=" + study.Running + " csvFiles=" + files.Length + " last=" + last);
            if (last != "") { var lines = File.ReadAllLines(last); Debug.Log("HERITAGE_STUDY_CSV columns=" + lines[0].Split(',').Length + " values=" + lines[1].Split(',').Length + " head=" + lines[0].Substring(0,Mathf.Min(260,lines[0].Length)) + " row=" + lines[1].Substring(0,Mathf.Min(260,lines[1].Length))); }
        }

        // Flood-fills where the walking capsule can reach (same rules as the rig) and reports which targets are reachable.
        static string[] Reach(Vector3 start, Vector3[] targets)
        {
            const float step = .36f, radius = .25f, eye = 1.7f, cell = .15f;
            var seen = new System.Collections.Generic.HashSet<Vector2Int>(); var q = new System.Collections.Generic.Queue<Vector2Int>(); var y = new System.Collections.Generic.Dictionary<Vector2Int,float>();
            Vector2Int K(float x,float z) => new Vector2Int(Mathf.RoundToInt(x/cell),Mathf.RoundToInt(z/cell));
            var s0 = K(start.x,start.z); seen.Add(s0); y[s0] = 0; q.Enqueue(s0);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var n = c + d; if (seen.Contains(n)) continue;
                    var tg = new Vector3(n.x*cell,y[c],n.y*cell); if (Mathf.Abs(tg.x) > 16.6f || tg.z < -34f || tg.z > 22f) continue;
                    if (!Physics.Raycast(tg + Vector3.up*(step+.05f),Vector3.down,out var hit,step+1.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                    tg.y = hit.point.y;
                    if (Physics.CheckCapsule(tg + Vector3.up*(step+radius),tg + Vector3.up*(eye-radius+.05f),radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                    seen.Add(n); y[n] = tg.y; q.Enqueue(n);
                }
            }
            var res = new string[targets.Length];
            for (int i = 0; i < targets.Length; i++) res[i] = "z=" + targets[i].z + (seen.Contains(K(targets[i].x,targets[i].z)) ? " OK" : " BLOCKED");
            return res;
        }

        static Shot[] Shots()
        {
            return new[] {
                S("00-hub-overview",-1,false,new Vector3(10,0,-14),new Vector3(-2.4f,1.6f,3),7),
                S("10-belur-overview",0,false,new Vector3(10,0,-17),new Vector3(0,2,3),6),
                S("11-belur-approach",0,false,new Vector3(0,0,-30),new Vector3(0,3,0),1.7f),
                S("12-belur-hall",0,false,new Vector3(0,0,-9),new Vector3(0,2,6),1.7f),
                S("13-belur-gate",0,false,new Vector3(0,0,-19),new Vector3(0,3,0),1.7f),
                S("14-belur-sanctum",0,false,new Vector3(0,0,1.5f),new Vector3(0,1.5f,6),1.7f),
                S("40-belur-steps",0,false,new Vector3(0,0,-3.5f),new Vector3(0,1.5f,3),1.7f),
                S("41-belur-hall-inside",0,false,new Vector3(0,.82f,2.2f),new Vector3(0,1.8f,6),1.7f),
                S("42-belur-sanctum-inside",0,false,new Vector3(0,.82f,6.0f),new Vector3(0,1.4f,7.4f),1.7f),
                S("43-belur-sanctum-door",0,false,new Vector3(0,.82f,4.2f),new Vector3(0,1.6f,6.5f),1.7f),
                S("50-halebidu-steps",1,false,new Vector3(-3.9f,0,-3.5f),new Vector3(-3.9f,1.5f,3),1.7f),
                S("51-halebidu-hall-inside",1,false,new Vector3(-3.9f,.82f,2.2f),new Vector3(-3.9f,1.8f,6),1.7f),
                S("52-halebidu-sanctum-inside",1,false,new Vector3(-3.9f,.82f,6.0f),new Vector3(-3.9f,1.4f,7.4f),1.7f),
                S("15-belur-then",0,true,new Vector3(10,0,-17),new Vector3(0,2,3),6),
                S("20-halebidu-overview",1,false,new Vector3(10,0,-17),new Vector3(0,2,3),6),
                S("30-hampi-overview",2,false,new Vector3(10,0,-17),new Vector3(0,2,3),6),
                S("31-hampi-court",2,false,new Vector3(0,0,-12),new Vector3(0,4,8),1.7f),
            };
        }
        static Shot S(string n,int t,bool then,Vector3 f,Vector3 l,float h) { return new Shot{name=n,temple=t,then=then,feet=f,look=l,height=h}; }

        IEnumerator Capture(string name)
        {
            yield return null;
            var camera = app.rig.view; int w = 1600, h = 900;
            var rt = new RenderTexture(w,h,24,RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            var old = camera.targetTexture; camera.targetTexture = rt; camera.aspect = w/(float)h;
            camera.Render(); camera.targetTexture = old; camera.ResetAspect();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w,h,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,w,h),0,0); tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
            Destroy(tex); rt.Release(); Destroy(rt);
        }
    }
}
