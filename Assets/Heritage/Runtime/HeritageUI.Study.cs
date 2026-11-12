using UnityEngine;
using UnityEngine.UI;

namespace VirtualHeritage
{
    // Audio-guide controls and captions, touch-screen menu toggle, and the student-study wizard screens.
    public sealed partial class HeritageUI
    {
        RectTransform captionRect; Text captionText, footerHint, studyTimer, studyFinishLabel;
        Button touchToggle, studyFinish;
        bool panelHidden, touchMode;

        void InitGuideUI()
        {
            captionRect = Rect("Guide caption",transform,0,0,880,92);
            captionRect.anchorMin = captionRect.anchorMax = captionRect.pivot = new Vector2(.5f,0); captionRect.anchoredPosition = new Vector2(150,60);
            Background(captionRect,new Color(.075f,.11f,.105f,.9f));
            captionText = Text(captionRect,"",16,8,848,76,20,paper); captionText.alignment = TextAnchor.MiddleCenter;
            captionRect.gameObject.SetActive(false);
            app.guide.CaptionChanged += () => SetCaption(app.guide.Caption);
            touchToggle = Button(header,"Hide menu",520,20,150,36,TogglePanel); touchToggle.gameObject.SetActive(false);
        }

        public void SetCaption(string value)
        {
            if (!captionRect) return;
            captionText.text = value ?? "";
            captionRect.gameObject.SetActive(!vr && !string.IsNullOrEmpty(value));
        }

        // ---------- Touch screens: the panel covers a lot of a phone, so it can be hidden ----------

        public void SetTouchMode(bool on)
        {
            if (touchMode == on) return;
            touchMode = on; touchToggle.gameObject.SetActive(on && !vr);
            if (on) footerHint.text = "DRAG LEFT SIDE TO WALK   /   DRAG RIGHT SIDE TO LOOK   /   TAP RINGS TO TELEPORT   /   MENU BUTTON HIDES THE PANEL";
        }

        void TogglePanel()
        {
            panelHidden = !panelHidden; panel.gameObject.SetActive(!panelHidden);
            touchToggle.GetComponentInChildren<Text>().text = panelHidden ? "Show menu" : "Hide menu";
        }

        void ShowPanel() { if (!panelHidden) return; panelHidden = false; panel.gameObject.SetActive(true); touchToggle.GetComponentInChildren<Text>().text = "Hide menu"; }

        // True when a screen position is on a button or panel, so touch gestures there are not treated as walking or looking.
        public bool IsOverUI(Vector2 screenPoint)
        {
            if (vr || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return false;
            if (!panelHidden && panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(panel,screenPoint,null)) return true;
            return touchToggle && touchToggle.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)touchToggle.transform,screenPoint,null);
        }

        // ---------- Audio guide row (panel bottom) ----------

        void GuideRow()
        {
            if (app.Study.Exploring) { StudyRow(); return; }
            var g = app.guide; Text onLabel = null, languageLabel = null;
            var on = Button(panel,g.Enabled ? "Guide on" : "Guide off",24,706,104,32,() => { g.SetEnabled(!g.Enabled); onLabel.text = g.Enabled ? "Guide on" : "Guide off"; });
            onLabel = on.GetComponentInChildren<Text>();
            var language = Button(panel,g.LanguageName,134,706,124,32,() => { g.CycleLanguage(); languageLabel.text = g.LanguageName; });
            languageLabel = language.GetComponentInChildren<Text>();
            Button(panel,"Replay",264,706,104,32,g.Replay);
        }

        // ---------- Study HUD during free exploration ----------

        void StudyRow()
        {
            studyTimer = Text(panel,"STUDY",24,712,160,24,15,jade,FontStyle.Bold);
            studyFinish = Button(panel,"Finish",190,706,178,32,() => app.Study.Finish(),true);
            studyFinishLabel = studyFinish.GetComponentInChildren<Text>();
            UpdateStudyHud(app.Study.Elapsed,app.Study.MinSeconds,app.Study.CanFinish);
        }

        static string Clock(float seconds) { int s = Mathf.Max(0,Mathf.CeilToInt(seconds)); return (s / 60) + ":" + (s % 60).ToString("00"); }

        public void UpdateStudyHud(float elapsed,float minimum,bool canFinish)
        {
            if (studyTimer) studyTimer.text = "STUDY  " + Clock(elapsed);
            if (studyFinish) { studyFinish.interactable = canFinish; studyFinishLabel.text = canFinish ? "Finish" : "Finish in " + Clock(minimum - elapsed); }
        }

        public void RestoreAfterStudy()
        {
            if (app.TempleIndex >= 0 && app.SelectedIndex >= 0) ShowElement();
            else if (app.TempleIndex >= 0) ShowTemple();
            else ShowHub();
        }

        // ---------- Study wizard page ----------

        static float Lines(string text,int charsPerLine,float lineHeight)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float lines = 0; foreach (var part in text.Split('\n')) lines += Mathf.Max(1,Mathf.CeilToInt(part.Length / (float)charsPerLine));
            return lines * lineHeight + 8;
        }

        public void ShowStudyPage(StudyPage page)
        {
            Clear(); ShowPanel();
            Eyebrow(page.eyebrow);
            float titleHeight = Mathf.Min(170,Lines(page.title,page.title.Length > 70 ? 28 : 24,page.title.Length > 70 ? 28 : 33));
            Text(panel,page.title,24,54,344,titleHeight,page.title.Length > 70 ? 22 : 26,paper,FontStyle.Bold);
            float y = 54 + titleHeight + 8;
            if (!string.IsNullOrEmpty(page.body))
            {
                float bodyHeight = Lines(page.body,40,21);
                Text(panel,page.body,24,y,344,bodyHeight,16,muted); y += bodyHeight + 14;
            }
            if (page.keypad)
            {
                Text(panel,page.code.PadRight(3,'_').Replace("_"," _").Trim(),24,y,344,56,40,jade,FontStyle.Bold); y += 64;
                string[] keys = { "1","2","3","4","5","6","7","8","9","Clear","0","OK" };
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i]; float x = 24 + (i % 3) * 118, ky = y + (i / 3) * 58;
                    var b = Button(panel,key,x,ky,108,50,() =>
                    {
                        if (key == "Clear") page.code = "";
                        else if (key == "OK") { if (page.code.Length == 3) { page.onCode(page.code); return; } }
                        else if (page.code.Length < 3) page.code += key;
                        ShowStudyPage(page);
                    },key == "OK");
                    if (key == "OK") b.interactable = page.code.Length == 3;
                }
            }
            foreach (var option in page.options)
            {
                var action = option.action;
                var b = Button(panel,option.label,24,y,344,page.optionHeight,() => action(),option.primary);
                y += page.optionHeight + 8;
            }
            if (app.Study.Running) Button(panel,"Stop study",24,706,160,32,() => app.Study.Abort());
        }
    }
}
