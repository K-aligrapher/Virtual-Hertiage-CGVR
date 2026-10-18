using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Management;

namespace VirtualHeritage
{
    public sealed class HeritageApp : MonoBehaviour
    {
        public TempleCatalog Catalog { get; private set; }
        public HeritageRig rig;
        public HeritageUI ui;
        public int TempleIndex { get; private set; } = -1;
        public int TimelineIndex { get; private set; } = 3;
        public int SelectedIndex { get; private set; } = -1;
        public TempleRecord CurrentTemple => TempleIndex < 0 ? null : Catalog.temples[TempleIndex];
        public HeritageElement SelectedElement => SelectedIndex < 0 ? null : CurrentTemple.elements[SelectedIndex];
        public bool Then { get; private set; }
        public bool Inspecting => inspectionRoot;
        public bool Wireframe { get; private set; }
        public bool Transitioning { get; private set; }
        public bool Muted => audioScene && audioScene.Muted;
        public int VisitedCount => visited.Count;
        public Material RayMaterial { get; private set; }
        public IReadOnlyList<Transform> ElementObjects => elements;
        public IReadOnlyList<Vector3> LandingPoints => landings;
        public float TemporalBlend { get; private set; }

        readonly HashSet<int> visited = new HashSet<int>();
        readonly List<Transform> elements = new List<Transform>();
        readonly List<Vector3> landings = new List<Vector3>();
        readonly List<Mesh> wireMeshes = new List<Mesh>();
        readonly List<Renderer> solidRenderers = new List<Renderer>();
        readonly List<GameObject> wireObjects = new List<GameObject>();
        readonly List<Transform> labels = new List<Transform>();
        HeritageGeometry geometry;
        HeritageAudio audioScene;
        Transform world, reconstruction, inspectionRoot, inspectionPivot;
        Material wireMaterial, sky;
        Light sun;
        Vector3 inspectionScale;
        float zoom = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!FindAnyObjectByType<HeritageApp>()) new GameObject("Virtual Heritage Karnataka").AddComponent<HeritageApp>();
        }

        void Start()
        {
            Catalog = TempleCatalog.Load();
            Application.targetFrameRate = 90; QualitySettings.vSyncCount = 0;
            RayMaterial = new Material(HeritageGeometry.FindShader("Universal Render Pipeline/Unlit")) { color = new Color(.3f,.9f,.74f) };
            wireMaterial = new Material(RayMaterial) { name = "Inspection wireframe", color = new Color(.3f,1,.8f) };
            SetupLighting();
            rig = new GameObject("Rig").AddComponent<HeritageRig>(); rig.transform.SetParent(transform,false); rig.Initialize(this);
            HeritageAtmosphere.PostProcessing(rig.view);
            rig.ForceDesktop = Array.Exists(Environment.GetCommandLineArgs(),a => a == "--desktop" || a == "--heritage-smoke");
#if UNITY_WEBGL
            rig.ForceDesktop = true; // Browsers run the desktop experience; VR is the Windows build.
#endif
            audioScene = new GameObject("Original spatial soundscape").AddComponent<HeritageAudio>(); audioScene.transform.SetParent(transform,false); audioScene.Initialize();
            ui = new GameObject("Heritage interface",typeof(RectTransform)).AddComponent<HeritageUI>(); ui.transform.SetParent(transform,false); ui.Initialize(this);
            BuildLocation(-1);
            if (!rig.ForceDesktop) StartCoroutine(StartXR());
            if (Array.Exists(Environment.GetCommandLineArgs(),a => a == "--heritage-smoke")) gameObject.AddComponent<HeritageSmoke>();
            if (Array.Exists(Environment.GetCommandLineArgs(),a => a == "--heritage-shots")) { rig.ForceDesktop = true; gameObject.AddComponent<HeritageShots>(); }
        }

        IEnumerator StartXR()
        {
            var manager = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            if (!manager) yield break;
            yield return manager.InitializeLoader();
            if (manager.activeLoader) manager.StartSubsystems();
        }

        void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f,.73f,.79f);
            RenderSettings.ambientEquatorColor = new Color(.42f,.47f,.44f);
            RenderSettings.ambientGroundColor = new Color(.23f,.27f,.23f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.65f,.75f,.75f); RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 32; RenderSettings.fogEndDistance = 105;
            sun = new GameObject("Late afternoon sun").AddComponent<Light>(); sun.transform.SetParent(transform,false);
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(42,-32,0);
            sun.color = new Color(1,.95f,.83f); sun.intensity = 1.6f; sun.shadows = LightShadows.Soft;
            sun.shadowBias = .035f; sun.shadowNormalBias = .25f;
            RenderSettings.sun = sun;
            sky = new Material(HeritageGeometry.FindShader("Skybox/Procedural"));
            sky.SetColor("_SkyTint",new Color(.6f,.6f,.68f)); sky.SetColor("_GroundColor",new Color(.45f,.37f,.29f));
            sky.SetFloat("_AtmosphereThickness",1.05f); sky.SetFloat("_Exposure",1.25f);
            sky.SetFloat("_SunSize",.05f); sky.SetFloat("_SunSizeConvergence",6); RenderSettings.skybox = sky;
            HeritageAtmosphere.Lighting(sun);
            sun.shadowBias = .02f; sun.shadowNormalBias = .35f;
            DynamicGI.UpdateEnvironment();
        }

        public void OpenTemple(int index)
        {
            if (index < 0 || index >= Catalog.temples.Length || Transitioning) return;
            StartCoroutine(ChangeLocation(index));
        }

        public void OpenHub()
        {
            if (Transitioning) return;
            if (TempleIndex == -1) { Overview(); return; }
            StartCoroutine(ChangeLocation(-1));
        }

        IEnumerator ChangeLocation(int index)
        {
            Transitioning = true;
            yield return Fade(0,1,.16f);
            BuildLocation(index);
            yield return null;
            yield return Fade(1,0,.24f);
            Transitioning = false;
        }

        IEnumerator Fade(float from,float to,float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                var color = ui.Fade.color; color.a = Mathf.Lerp(from,to,t/duration); ui.Fade.color = color; rig.TransitionFade = color.a;
                yield return null;
            }
            var end = ui.Fade.color; end.a = to; ui.Fade.color = end; rig.TransitionFade = to;
        }

        void BuildLocation(int index)
        {
            float started = Time.realtimeSinceStartup;
            CloseInspection(false); SelectedIndex = -1; labels.Clear(); elements.Clear(); landings.Clear();
            if (world) { world.gameObject.SetActive(false); Destroy(world.gameObject); }
            geometry?.Dispose(); geometry = new HeritageGeometry();
            world = geometry.Group("Current collection environment",transform,Vector3.zero).transform;
            TempleIndex = index; Then = false; TemporalBlend = 0; TimelineIndex = 3;
            reconstruction = geometry.Group("Illustrative restored accents",world,Vector3.zero).transform;
            HeritageAtmosphere.Garden(geometry,world,index + 42);
            HeritageAtmosphere.Compound(geometry,world,index >= 0);
            if (index < 0) BuildHub(); else { visited.Add(index); BuildTemple(index); }
            reconstruction.gameObject.SetActive(false);
            Debug.Log("HERITAGE: built location " + index + " in " + (Time.realtimeSinceStartup - started).ToString("0.00") + " s");
            if (index < 0) ui.ShowHub(); else ui.ShowTemple();
            Overview();
        }

        void BuildHub()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = (i-1)*7.2f;
                var exhibit = geometry.Group(Catalog.temples[i].name + " entrance",world,new Vector3(x,0,3));
                geometry.Box("Gallery plinth",exhibit.transform,new Vector3(0,.4f,1.1f),new Vector3(5.5f,.8f,5.2f),geometry.Dark);
                geometry.Box("Brass rim",exhibit.transform,new Vector3(0,.83f,1.1f),new Vector3(5.52f,.045f,5.22f),geometry.Brass,false);
                var miniature = geometry.Temple(exhibit.transform,i,true); miniature.transform.localPosition = new Vector3(0,.85f,i == 2 ? -.1f : -.45f);
                var collider = exhibit.AddComponent<BoxCollider>(); collider.center = new Vector3(0,1.3f,1.1f); collider.size = new Vector3(5.5f,2.6f,5.2f);
                exhibit.AddComponent<HeritageTarget>().Configure(this,HeritageTargetKind.Temple,i);
                Label("0"+(i+1)+"  /  "+new[]{"BELUR","HALEBIDU","HAMPI"}[i],new Vector3(x,4.8f,3.8f),.08f);
            }
            AddLanding(new Vector3(0,0,-7)); AddLanding(new Vector3(-8,0,-3)); AddLanding(new Vector3(8,0,-3));
            geometry.Ring(world,new Vector3(0,0,-1),2.3f,geometry.Brass);
        }

        void BuildTemple(int index)
        {
            geometry.Temple(world,index,false,reconstruction);
            if (index == 2)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int j = 0; j < 4; j++)
                        HeritageAtmosphere.Garland(geometry,world,new Vector3(side * 5.2f,3.45f,-.4f + j * 2.2f),new Vector3(side * 5.2f,3.45f,-.4f + (j + 1) * 2.2f),.32f,10);
                HeritageAtmosphere.Lamp(geometry,world,new Vector3(0,0,5.2f),true,2.2f);
            }
            else foreach (var door in geometry.Doors) HeritageAtmosphere.Sanctum(geometry,world,door);
            for (int i = 0; i < 3; i++)
            {
                float x = (i-1)*5.3f;
                geometry.Box("Study plinth " + i,world,new Vector3(x,.16f,-4),new Vector3(2.2f,.32f,2.1f),geometry.Dark);
                geometry.Box("Study plinth rim " + i,world,new Vector3(x,.33f,-4),new Vector3(2.24f,.03f,2.14f),geometry.Brass,false);
                Transform model;
                if (i == 0) model = geometry.Pillar(world,new Vector3(x,.32f,-4),index == 2).transform;
                else if (i == 1 && index == 2) model = geometry.Tower(world,new Vector3(x,.32f,-4),.25f).transform;
                else if (i == 1 && index == 1) model = geometry.ShrineStudy(world,new Vector3(x,.32f,-4)).transform;
                else if (i == 1) model = geometry.Star(world,new Vector3(x,.32f,-4),1.9f,.55f).transform;
                else if (index == 2) model = geometry.CorniceStudy(world,new Vector3(x,.32f,-4)).transform;
                else { model = geometry.Frieze(world,new Vector3(x,.32f,-4)).transform; model.localScale = Vector3.one * .65f; }
                model.gameObject.AddComponent<HeritageTarget>().Configure(this,HeritageTargetKind.Element,i);
                elements.Add(model);
                Label("0"+(i+1)+"  /  "+CurrentTemple.elements[i].name.ToUpperInvariant(),new Vector3(x,.5f,-5.7f),.026f);
                geometry.Ring(world,new Vector3(x,.004f,-4),1.38f,geometry.Teal);
            }
            float flank = index == 1 ? 12.6f : 11;
            AddLanding(new Vector3(0,0,-9)); AddLanding(new Vector3(-8,0,-2)); AddLanding(new Vector3(8,0,-2));
            AddLanding(new Vector3(-flank,0,6)); AddLanding(new Vector3(flank,0,6));
            // The Then layer shows illustrative superstructures and festive textiles. It is not a measured reconstruction.
            for (int s = -1; s <= 1; s += 2)
            {
                geometry.Box("Illustrative festive textile",reconstruction,new Vector3(s*13,2.7f,-.5f),new Vector3(.85f,2.8f,.05f),geometry.Saffron,false);
                geometry.Box("Textile support",world,new Vector3(s*13,2.4f,-.5f),new Vector3(.075f,4.8f,.075f),geometry.Brass);
            }
        }

        void AddLanding(Vector3 position)
        {
            var landing = geometry.Ring(world,position,.56f,geometry.Teal);
            var collider = landing.AddComponent<BoxCollider>(); collider.center = new Vector3(0,.04f,0); collider.size = new Vector3(1.14f,.12f,1.14f);
            landing.AddComponent<HeritageTarget>().Configure(this,HeritageTargetKind.Teleport,landings.Count);
            landings.Add(position);
        }

        void Label(string text,Vector3 position,float size)
        {
            var go = geometry.Group(text,world,position);
            var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size; mesh.fontSize = 80;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.GetComponent<Renderer>().sharedMaterial = mesh.font.material;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = new Color(1f,.9f,.68f);
            labels.Add(go.transform);
        }

        public void Overview()
        {
            CloseInspection(false);
            if (rig.IsVR) rig.Place(new Vector3(0,0,-9),new Vector3(0,1.7f,5));
            else if (TempleIndex < 0) rig.Place(new Vector3(10,0,-14),new Vector3(-2.4f,1.6f,3),7);
            else rig.Place(new Vector3(10,0,-17),new Vector3(0,2,3),6);
            ui.Recenter();
        }

        public void SelectElement(int index,Transform selected = null)
        {
            if (CurrentTemple == null || index < 0 || index >= elements.Count || Transitioning) return;
            CloseInspection(false); SelectedIndex = index; ui.ShowElement(); PlayClick();
        }

        public void ClearSelection() { CloseInspection(false); SelectedIndex = -1; ui.ShowTemple(); }

        public void ToggleInspection()
        {
            if (SelectedIndex < 0) return;
            if (Inspecting) { CloseInspection(); return; }
            var selected = elements[SelectedIndex]; selected.GetComponent<HeritageTarget>().Highlight(false);
            inspectionRoot = new GameObject("Inspection stage").transform; inspectionRoot.SetParent(transform,false);
            var camera = rig.view.transform;
            Vector3 forward = rig.IsVR ? Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized : camera.forward;
            Vector3 right = Vector3.Cross(Vector3.up,forward).normalized;
            inspectionRoot.position = camera.position + forward * (rig.IsVR ? 2.4f : 3.5f) + right * .55f - camera.up * .15f;
            inspectionRoot.rotation = Quaternion.LookRotation(forward,Vector3.up);
            inspectionPivot = new GameObject("Rotate and scale pivot").transform; inspectionPivot.SetParent(inspectionRoot,false);
            var clone = Instantiate(selected.gameObject,inspectionPivot);
            clone.name = "Inspection copy"; clone.transform.localPosition = Vector3.zero; clone.transform.localRotation = Quaternion.identity;
            foreach (var target in clone.GetComponentsInChildren<HeritageTarget>()) { target.enabled = false; Destroy(target); }
            foreach (var xr in clone.GetComponentsInChildren<XRSimpleInteractable>()) { xr.enabled = false; Destroy(xr); }
            foreach (var col in clone.GetComponentsInChildren<Collider>()) { col.enabled = false; Destroy(col); }
            var renderers = clone.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(renderers[0].bounds.center,Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float factor = 1.85f / Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            clone.transform.localScale *= factor;
            bounds = new Bounds(renderers[0].bounds.center,Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            clone.transform.position += inspectionRoot.position - bounds.center;
            inspectionScale = inspectionPivot.localScale; zoom = 1; Wireframe = false;
            solidRenderers.Clear(); solidRenderers.AddRange(renderers);
            MakeWireframe(clone.transform);
            ui.ShowElement();
        }

        void MakeWireframe(Transform root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh; if (!source || !source.isReadable) continue;
                var triangles = source.triangles; var edges = new HashSet<ulong>(); var indices = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = triangles[i+e], b = triangles[i+(e+1)%3];
                        uint lo = (uint)Mathf.Min(a,b), hi = (uint)Mathf.Max(a,b); ulong key = ((ulong)lo << 32) | hi;
                        if (edges.Add(key)) { indices.Add(a); indices.Add(b); }
                    }
                var mesh = new Mesh { name = "Inspection mesh edges", indexFormat = source.indexFormat }; mesh.vertices = source.vertices; mesh.SetIndices(indices.ToArray(),MeshTopology.Lines,0); mesh.RecalculateBounds(); wireMeshes.Add(mesh);
                var go = new GameObject("Wire edges"); go.transform.SetParent(filter.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = wireMaterial;
                go.SetActive(false); wireObjects.Add(go);
            }
        }

        public void CloseInspection(bool refresh = true)
        {
            if (inspectionRoot) { inspectionRoot.gameObject.SetActive(false); Destroy(inspectionRoot.gameObject); }
            inspectionRoot = inspectionPivot = null; solidRenderers.Clear(); wireObjects.Clear();
            foreach (var mesh in wireMeshes) Destroy(mesh); wireMeshes.Clear(); Wireframe = false;
            if (refresh && ui && SelectedIndex >= 0) ui.ShowElement();
        }

        public void RotateInspection(float degrees) { if (inspectionPivot) inspectionPivot.Rotate(Vector3.up,degrees,Space.Self); }
        public void ZoomInspection(float delta) { if (inspectionPivot) { zoom = Mathf.Clamp(zoom + delta,.55f,1.5f); inspectionPivot.localScale = inspectionScale * zoom; } }
        public void ToggleWireframe()
        {
            if (!Inspecting) return; Wireframe = !Wireframe;
            foreach (var renderer in solidRenderers) if (renderer) renderer.enabled = !Wireframe;
            foreach (var wire in wireObjects) if (wire) wire.SetActive(Wireframe);
            ui.ShowElement();
        }

        public void SetThen(bool value) { if (CurrentTemple == null) return; Then = value; ui.UpdatePeriod(); }
        public void SetTimeline(int index) { if (CurrentTemple == null) return; TimelineIndex = Mathf.Clamp(index,0,CurrentTemple.timeline.Length-1); ui.UpdateTimeline(); }
        public void ToggleAudio() { audioScene.Toggle(); }
        public void PlayClick() { if (audioScene) audioScene.PlayClick(); }

        // Now / Then finishes: weathered stone and plaster versus freshly dressed surfaces.
        // Belur and Halebidu are chloritic soapstone; Hampi is granite.
        static readonly Color[][] Palettes = {
            new[] { new Color(.40f,.38f,.33f), new Color(.55f,.52f,.45f), new Color(.5f,.48f,.42f), new Color(.66f,.63f,.55f), new Color(.8f,.76f,.66f), new Color(.95f,.92f,.84f) },
            new[] { new Color(.56f,.48f,.39f), new Color(.67f,.59f,.49f), new Color(.64f,.57f,.48f), new Color(.75f,.68f,.58f), new Color(.82f,.77f,.66f), new Color(.97f,.94f,.86f) } };

        void Update()
        {
            if (geometry == null) return;
            TemporalBlend = Mathf.MoveTowards(TemporalBlend,Then ? 1 : 0,Time.deltaTime * 1.6f);
            var palette = Palettes[TempleIndex == 2 ? 1 : 0];
            geometry.Stone.color = Color.Lerp(palette[0],palette[1],TemporalBlend);
            geometry.Edge.color = Color.Lerp(palette[2],palette[3],TemporalBlend);
            geometry.Pale.color = Color.Lerp(palette[4],palette[5],TemporalBlend);
            sun.intensity = Mathf.Lerp(1.7f,1.9f,TemporalBlend);
            if (reconstruction) reconstruction.gameObject.SetActive(TemporalBlend > .5f);
        }

        void LateUpdate()
        {
            if (!rig || !rig.view) return;
            foreach (var label in labels) if (label) label.rotation = Quaternion.LookRotation(label.position - rig.view.transform.position,Vector3.up);
        }

        void OnDestroy()
        {
            geometry?.Dispose(); Destroy(RayMaterial); Destroy(wireMaterial); Destroy(sky);
            foreach (var mesh in wireMeshes) Destroy(mesh);
            HeritageGeometry.ReleaseTextures(); HeritageAtmosphere.Release();
        }
    }
}
