using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualHeritage
{
    // Opt-in executable journey checks, launched only with --heritage-smoke.
    public sealed class HeritageSmoke : MonoBehaviour
    {
        [Serializable] class Result { public int passed; public List<string> checks = new List<string>(); public List<string> failures = new List<string>(); }
        readonly Result result = new Result();
        HeritageApp app;
        string output;
        float started;
        bool finished;

        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message,string stack,LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) result.failures.Add(message);
        }
        void Check(bool condition,string label)
        {
            if (condition) { result.passed++; result.checks.Add(label); }
            else result.failures.Add(label);
        }
        void Update() { if (!finished && started > 0 && Time.realtimeSinceStartup - started > 100) { result.failures.Add("Journey timed out"); Finish(); } }

        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            app = GetComponent<HeritageApp>();
            output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts")); Directory.CreateDirectory(output);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--artifacts") { output = args[i+1]; Directory.CreateDirectory(output); }
            // Hidden Windows launches can initially have an unpresented swapchain. Resize before capture.
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            yield return new WaitForSeconds(.5f);
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);
            yield return new WaitForSeconds(1.5f);
            Check(app.TempleIndex == -1 && app.Catalog.temples.Length == 3,"Gallery loads three local temples");
            Check(!app.rig.IsVR,"Desktop fallback runs without an XR device");
            Check(GraphicsSettingsReady(),"URP renderer is active");
            yield return Capture("01-gallery");
            var galleryButton = Array.Find(app.ui.panel.GetComponentsInChildren<Button>(),b => b.GetComponentInChildren<Text>().text == "");
            Check(galleryButton != null,"Gallery entrance button is present");
            if (galleryButton) ExecuteEvents.Execute(galleryButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            yield return Settle();
            Check(app.TempleIndex == 0,"Gallery UI enters Belur");

            for (int temple = 0; temple < 3; temple++)
            {
                if (app.TempleIndex != temple) { app.OpenTemple(temple); yield return Settle(); }
                Check(app.TempleIndex == temple,"Enter temple " + temple);
                Check(app.ElementObjects.Count == 3 && app.LandingPoints.Count == 5,"Three elements and five landing points at temple " + temple);
                Check(app.CurrentTemple.elements[0].historicalConfidence.Length > 0,"Historical confidence metadata at temple " + temple);
                yield return Capture("0"+(temple+2)+"-"+app.CurrentTemple.id);
                for (int element = 0; element < 3; element++)
                {
                    var source = app.ElementObjects[element]; var position = source.position; var scale = source.localScale;
                    source.GetComponent<HeritageTarget>().Activate();
                    Check(app.SelectedIndex == element,"Select element " + temple + ":" + element);
                    app.ToggleInspection(); yield return null;
                    Check(app.Inspecting,"Inspection opens " + temple + ":" + element);
                    app.RotateInspection(45); app.ZoomInspection(.2f); app.ToggleWireframe();
                    Check(app.Wireframe,"Wireframe toggles " + temple + ":" + element);
                    if (temple == 0 && element == 0) yield return Capture("05-wireframe");
                    app.ToggleWireframe();
                    if (temple == 0 && element == 0) yield return Capture("06-inspection");
                    app.CloseInspection();
                    Check(source.position == position && source.localScale == scale,"Inspection preserves original " + temple + ":" + element);
                }
                app.ClearSelection();
                for (int era = 0; era < 4; era++) { app.ui.TimelineSlider.value = era; Check(app.TimelineIndex == era,"Timeline " + temple + ":" + era); }
                app.SetThen(true); yield return new WaitForSeconds(.8f);
                Check(app.Then && app.TemporalBlend > .99f,"Then state fully transitions " + temple);
                if (temple == 2) yield return Capture("07-then");
                app.SetThen(false); yield return new WaitForSeconds(.8f);
                Check(!app.Then && app.TemporalBlend < .01f,"Now state fully transitions " + temple);
                Vector3 destination = app.LandingPoints[2];
                Check(app.rig.Teleport(destination),"XRI accepts teleport " + temple);
                yield return new WaitForSeconds(.3f);
                var camera = app.rig.view.transform.position;
                Check(Vector2.Distance(new Vector2(camera.x,camera.z),new Vector2(destination.x,destination.z)) < .2f,"XRI moves camera to destination " + temple);
                app.OpenHub(); yield return Settle();
                Check(app.TempleIndex == -1 && !app.Inspecting,"Return to gallery " + temple);
            }
            Check(app.VisitedCount == 3,"All three temples recorded as visited");
            app.ToggleAudio(); Check(app.Muted,"Audio mute"); app.ToggleAudio(); Check(!app.Muted,"Audio unmute");
            app.ui.SetVR(true); yield return null;
            Check(app.ui.canvas.renderMode == RenderMode.WorldSpace,"VR interface uses a world-space canvas");
            app.ui.SetVR(false); yield return null;
            Check(app.ui.canvas.renderMode == RenderMode.ScreenSpaceOverlay,"Desktop interface restored");
            Screen.SetResolution(1280,720,FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
            yield return Capture("08-gallery-720p");
            Finish();
        }

        // Location changes generate geometry; wait for the fade to finish rather than a fixed delay.
        IEnumerator Settle()
        {
            yield return null;
            for (float t = 0; t < 20 && app.Transitioning; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(.3f);
        }

        bool GraphicsSettingsReady() { return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null; }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output,name+".png");
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            var pixels = image.GetPixels32(); int visible = 0, magenta = 0;
            var colors = new HashSet<int>();
            for (int i = 0; i < pixels.Length; i += 16)
            {
                var c = pixels[i]; if (c.r+c.g+c.b > 35) visible++;
                if (c.r > 220 && c.b > 220 && c.g < 40) magenta++;
                colors.Add((c.r/16)*256 + (c.g/16)*16 + c.b/16);
            }
            Check(visible > pixels.Length/64 && colors.Count > 30,"Nonblank rendered frame: " + name);
            Check(magenta < pixels.Length/1600,"No missing-shader pixels: " + name);
            File.WriteAllBytes(path,image.EncodeToPNG()); Destroy(image);
            yield return new WaitForSeconds(.15f);
        }

        void Finish()
        {
            if (finished) return; finished = true;
            File.WriteAllText(Path.Combine(output,"smoke-results.json"),JsonUtility.ToJson(result,true));
            Debug.Log("HERITAGE_SMOKE " + result.passed + " passed, " + result.failures.Count + " failed.");
            Application.Quit(result.failures.Count == 0 ? 0 : 1);
        }
    }
}
