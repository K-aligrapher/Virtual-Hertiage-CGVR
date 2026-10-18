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
            Debug.Log("HERITAGE_SHOTS done");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
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
