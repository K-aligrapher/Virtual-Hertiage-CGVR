using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VirtualHeritage
{
    public sealed class HeritageUI : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform panel;
        public Slider TimelineSlider { get; private set; }
        public Image Fade { get; private set; }
        readonly Color ink = new Color(.075f,.11f,.105f,.97f);
        readonly Color paper = new Color(.94f,.96f,.92f);
        readonly Color muted = new Color(.64f,.72f,.69f);
        readonly Color jade = new Color(.37f,.88f,.72f);
        readonly Color line = new Color(.23f,.30f,.28f);
        HeritageApp app;
        Font font;
        RectTransform header, footer;
        Text status, timelineDate, timelineTitle, timelineBody, periodState, volumeLabel;
        Button thenButton, nowButton;
        bool vr;

        public void Initialize(HeritageApp owner)
        {
            app = owner; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .65f;
            gameObject.AddComponent<GraphicRaycaster>(); gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            header = Rect("Masthead", transform,0,0,0,76); StretchWidth(header);
            Background(header, ink);
            Text(header,"VIRTUAL HERITAGE",28,14,440,24,15,jade,FontStyle.Bold);
            Text(header,"KARNATAKA",28,39,440,25,19,paper,FontStyle.Bold);
            status = Text(header,"LOCAL COLLECTION   /   DESKTOP",0,27,370,25,13,muted);
            status.alignment = TextAnchor.MiddleRight; var sr = status.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1,1); sr.pivot = new Vector2(1,1); sr.anchoredPosition = new Vector2(-28,-27);

            footer = Rect("Collection footer",transform,0,0,0,48); StretchWidth(footer);
            footer.anchorMin = new Vector2(0,0); footer.anchorMax = new Vector2(1,0); footer.pivot = new Vector2(0,0); footer.anchoredPosition = Vector2.zero;
            Background(footer,ink);
            Text(footer,"WASD WALK   /   SHIFT RUN   /   RIGHT-DRAG LOOK   /   Q E TURN   /   CLICK RINGS TO TELEPORT   /   T THEN-NOW   /   ESC COLLECTION",28,15,900,24,12,jade);
            var note = Text(footer,"Illustrative models. Not archaeological reconstructions.",0,13,420,24,13,muted);
            note.alignment = TextAnchor.MiddleRight; note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(1,1); note.rectTransform.pivot = new Vector2(1,1); note.rectTransform.anchoredPosition = new Vector2(-28,-13);

            panel = Rect("Collection panel",transform,24,100,392,710); Background(panel,ink);
            var fadeRect = Rect("Transition veil",transform,0,0,0,0); fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one; fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
            Fade = fadeRect.gameObject.AddComponent<Image>(); Fade.color = new Color(.025f,.04f,.035f,0); Fade.raycastTarget = false;
            ShowHub();
        }

        RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name,typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent,false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1); r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return r;
        }

        void StretchWidth(RectTransform r) { r.anchorMax = new Vector2(1,1); r.sizeDelta = new Vector2(0,r.sizeDelta.y); }
        Image Background(RectTransform r, Color color) { var image = r.gameObject.AddComponent<Image>(); image.color = color; return image; }

        Text Text(Transform parent, string value, float x,float y,float w,float h,int size,Color color,FontStyle style = FontStyle.Normal)
        {
            var r = Rect(value.Length > 35 ? value.Substring(0,35) : value,parent,x,y,w,h);
            var text = r.gameObject.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = color; text.fontStyle = style; text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; return text;
        }

        Button Button(Transform parent,string label,float x,float y,float w,float h,UnityAction action,bool primary = false)
        {
            var r = Rect(label,parent,x,y,w,h); var background = Background(r,primary ? jade : line);
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = background;
            var colors = button.colors; colors.highlightedColor = new Color(.8f,1,.9f); colors.pressedColor = new Color(.6f,.85f,.75f); colors.selectedColor = Color.white; button.colors = colors;
            button.onClick.AddListener(() => { app.PlayClick(); action(); });
            var text = Text(r,label,10,0,w-20,h,16,primary ? ink : paper,FontStyle.Bold); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        void Clear()
        {
            for (int i = panel.childCount - 1; i >= 0; i--) { panel.GetChild(i).gameObject.SetActive(false); Destroy(panel.GetChild(i).gameObject); }
            TimelineSlider = null; timelineDate = timelineTitle = timelineBody = periodState = null;
            thenButton = nowButton = null;
        }

        void Rule(float y) { Background(Rect("Divider",panel,24,y,344,1),line); }
        void Eyebrow(string label, float y = 24) { Text(panel,label,24,y,344,22,12,jade,FontStyle.Bold); }

        public void ShowHub()
        {
            Clear(); Eyebrow("THE COLLECTION   /   01 - 03");
            Text(panel,"Places carved\nin time.",24,58,344,97,36,paper,FontStyle.Bold);
            Text(panel,"Three temples. Two architectural traditions.\nAn exploration of Karnataka in stone.",24,170,344,55,17,muted);
            for (int i = 0; i < app.Catalog.temples.Length; i++)
            {
                int index = i; var temple = app.Catalog.temples[i]; float y = 250 + i * 120;
                Rule(y - 8);
                var b = Button(panel,"",24,y,344,102,() => app.OpenTemple(index));
                b.image.color = new Color(.105f,.16f,.145f);
                Text(b.transform,"0" + (i + 1),14,14,32,24,14,jade);
                Text(b.transform,temple.name,52,10,266,31,23,paper,FontStyle.Bold);
                Text(b.transform,temple.location,52,44,266,22,15,muted);
                Text(b.transform,temple.tradition.ToUpperInvariant(),52,72,266,19,11,jade);
            }
            Text(panel,"" + app.VisitedCount + " / 3 PLACES VISITED",24,632,300,22,12,muted);
            AudioButton(660);
        }

        public void ShowTemple()
        {
            Clear(); var temple = app.CurrentTemple;
            Button(panel,"<  Collection",24,20,145,34,app.OpenHub);
            Text(panel,"0" + (app.TempleIndex + 1) + " / 03",275,29,92,20,12,jade).alignment = TextAnchor.UpperRight;
            Text(panel,temple.name,24,73,344,48,32,paper,FontStyle.Bold);
            Text(panel,temple.location + "  /  " + temple.tradition,24,127,344,49,15,muted);
            Text(panel,temple.introduction,24,185,344,102,17,paper);
            Eyebrow("ARCHITECTURAL ELEMENTS",298);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = Button(panel,"0" + (i + 1) + "   " + temple.elements[i].name,24,329 + i * 42,344,36,() => app.SelectElement(index));
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }
            Rule(462);
            BuildTimeline(477);
            thenButton = Button(panel,"Then",24,624,104,38,() => app.SetThen(true));
            nowButton = Button(panel,"Now",132,624,104,38,() => app.SetThen(false));
            Button(panel,"Overview",244,624,124,38,app.Overview);
            periodState = Text(panel,"",24,675,344,21,12,jade);
            UpdatePeriod();
        }

        void BuildTimeline(float y)
        {
            timelineDate = Text(panel,"",24,y,100,20,13,jade,FontStyle.Bold);
            timelineTitle = Text(panel,"",129,y,239,22,14,paper,FontStyle.Bold);
            timelineBody = Text(panel,"",24,y+33,344,68,15,muted);
            var r = Rect("Historical timeline",panel,24,y+109,344,23);
            var slider = r.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = app.CurrentTemple.timeline.Length-1; slider.wholeNumbers = true;
            Background(Rect("Track",r,0,9,344,3),line);
            var fillArea = Rect("Fill area",r,0,9,344,3);
            var fill = Rect("Progress",fillArea,0,0,0,0); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            Background(fill,jade); slider.fillRect = fill;
            var handleArea = Rect("Handle area",r,7,0,330,23);
            var handle = Rect("Handle",handleArea,0,4,14,14); var image = Background(handle,jade);
            handle.anchorMin = handle.anchorMax = new Vector2(0,.5f); handle.pivot = new Vector2(.5f,.5f);
            slider.handleRect = handle; slider.targetGraphic = image;
            slider.onValueChanged.AddListener(value => app.SetTimeline(Mathf.RoundToInt(value)));
            TimelineSlider = slider; slider.SetValueWithoutNotify(app.TimelineIndex); UpdateTimeline();
        }

        public void ShowElement()
        {
            Clear(); var element = app.SelectedElement;
            Button(panel,"<  " + app.CurrentTemple.name,24,20,240,34,app.ClearSelection);
            Eyebrow(app.Inspecting ? "OBJECT STUDY   /   INSPECTION" : "OBJECT STUDY   /   IN CONTEXT",76);
            Text(panel,element.name,24,112,344,91,32,paper,FontStyle.Bold);
            Text(panel,element.type,24,205,344,30,17,jade);
            Rule(248);
            Text(panel,"MATERIAL",24,265,100,20,11,muted);
            Text(panel,element.material,130,261,238,48,16,paper);
            Text(panel,"PERIOD",24,319,100,20,11,muted);
            Text(panel,element.period,130,315,238,43,16,paper);
            Text(panel,element.description,24,375,344,128,18,paper);
            Text(panel,element.historicalConfidence,24,511,344,43,13,jade);
            Button(panel,app.Inspecting ? "Return to context" : "Inspect object",24,574,344,44,app.ToggleInspection,true);
            if (app.Inspecting)
            {
                Button(panel,"<",24,633,48,40,() => app.RotateInspection(30));
                Button(panel,">",76,633,48,40,() => app.RotateInspection(-30));
                Button(panel,"-",140,633,48,40,() => app.ZoomInspection(-.15f));
                Button(panel,"+",192,633,48,40,() => app.ZoomInspection(.15f));
                Button(panel,app.Wireframe ? "Solid" : "Mesh",256,633,112,40,app.ToggleWireframe);
            }
            else Button(panel,"Then / Now",24,633,164,40,() => app.SetThen(!app.Then));
        }

        void AudioButton(float y)
        {
            var button = Button(panel,app.Muted ? "Audio off" : "Audio on",24,y,128,32,() => { app.ToggleAudio(); ShowHub(); });
            volumeLabel = button.GetComponentInChildren<Text>();
        }

        public void UpdateTimeline()
        {
            if (!timelineDate || app.CurrentTemple == null) return;
            var entry = app.CurrentTemple.timeline[app.TimelineIndex];
            timelineDate.text = entry.date; timelineTitle.text = entry.title; timelineBody.text = entry.description;
            TimelineSlider.SetValueWithoutNotify(app.TimelineIndex);
        }

        public void UpdatePeriod()
        {
            if (periodState) periodState.text = app.Then ? "THEN  /  ILLUSTRATIVE RECONSTRUCTION" : "NOW  /  SCHEMATIC PRESENT-DAY STUDY";
            if (thenButton) thenButton.image.color = app.Then ? jade : line;
            if (nowButton) nowButton.image.color = !app.Then ? jade : line;
            if (thenButton) thenButton.GetComponentInChildren<Text>().color = app.Then ? ink : paper;
            if (nowButton) nowButton.GetComponentInChildren<Text>().color = !app.Then ? ink : paper;
        }

        public void SetVR(bool active)
        {
            vr = active; header.gameObject.SetActive(!active); footer.gameObject.SetActive(!active);
            canvas.renderMode = active ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = app.rig.view;
            var scaler = GetComponent<CanvasScaler>(); scaler.enabled = !active;
            if (active)
            {
                var root = (RectTransform)transform; root.sizeDelta = new Vector2(392,710); root.localScale = Vector3.one * .0025f;
                panel.anchoredPosition = Vector2.zero; Recenter();
            }
            else { transform.localScale = Vector3.one; panel.anchoredPosition = new Vector2(24,-100); }
            status.text = active ? "LOCAL COLLECTION   /   OPENXR" : "LOCAL COLLECTION   /   DESKTOP";
        }

        public void Recenter()
        {
            if (!vr) return;
            var camera = app.rig.view.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up,forward);
            transform.position = camera.position + forward * 2.3f - right * .72f - Vector3.up * .05f;
            transform.rotation = Quaternion.LookRotation(forward,Vector3.up);
        }
    }
}
