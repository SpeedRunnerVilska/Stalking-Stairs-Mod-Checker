using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ModDetector
{
    [BepInPlugin("vilska.moddetector", "Mod Detector", "3.0.0")]
    [BepInProcess("The Stalking Stairs.exe")]
    public class ModDetectorPlugin : BaseUnityPlugin
    {
        private Text modText;
        private bool showPanel;
        private float anim;

        private Rect windowRect = new Rect(20, 200, 480, 500);
        private bool resizing;
        private Vector2 resizeStart;
        private Vector2 scrollMain;
        private Vector2 scrollList;

        private string search = "";

        private List<string> plugins = new List<string>();
        private Dictionary<string, PluginInfo> lookup = new Dictionary<string, PluginInfo>();
        private Dictionary<string, int> riskScores = new Dictionary<string, int>();
        private PluginInfo selected;

        private int themeIndex;

        // =========================
        void Awake()
        {
            ScanMods();
            StartCoroutine(CreateHUD());
        }

        // =========================
        void ScanMods()
        {
            string[] cheatWords = { "cheat", "trainer", "hack", "godmode", "aimbot" };

            foreach (var p in Chainloader.PluginInfos.Values)
            {
                if (p.Metadata.GUID == "vilska.moddetector") continue;

                string display = p.Metadata.Name + "  v" + p.Metadata.Version;
                plugins.Add(display);
                lookup[display] = p;

                int score = 0;
                string name = p.Metadata.Name.ToLower();
                string guid = p.Metadata.GUID.ToLower();

                foreach (string w in cheatWords)
                    if (name.Contains(w) || guid.Contains(w)) score += 40;

                if (guid.Contains("harmony")) score += 20;
                if (guid.Length < 8) score += 15;
                if (name.Length < 4) score += 10;

                riskScores[display] = Mathf.Clamp(score, 0, 100);
            }
        }

        // =========================
        IEnumerator CreateHUD()
        {
            yield return new WaitForSeconds(2f);

            GameObject canvasObj = new GameObject("ModDetectorCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject btn = new GameObject("Open");
            btn.transform.SetParent(canvasObj.transform);

            var img = btn.AddComponent<Image>();
            img.color = new Color(0, 0, 0, .6f);

            var button = btn.AddComponent<Button>();
            button.onClick.AddListener(() => showPanel = !showPanel);

            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);
            rect.sizeDelta = new Vector2(160, 36);

            var t = new GameObject("Text").AddComponent<Text>();
            t.transform.SetParent(btn.transform);
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = "Open Mod Panel";
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;

            CreateCredit(canvasObj.transform);
        }

        void CreateCredit(Transform p)
        {
            var g = new GameObject("Credit");
            g.transform.SetParent(p);
            var txt = g.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.supportRichText = true;
            txt.text = "Made By <color=#00ff7f>Vilska</color>";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.rectTransform.anchorMin = new Vector2(.5f, 0);
            txt.rectTransform.anchorMax = new Vector2(.5f, 0);
            txt.rectTransform.pivot = new Vector2(.5f, 0);
            txt.rectTransform.anchoredPosition = new Vector2(0, 10);
            txt.rectTransform.sizeDelta = new Vector2(300, 30);
        }

        void Update()
        {
            float target = showPanel ? 1 : 0;
            anim = Mathf.Lerp(anim, target, Time.deltaTime * 8);
        }

        // =========================
        void OnGUI()
        {
            if (anim < 0.05f) return;

            ApplyTheme();

            GUI.color = new Color(0, 0, 0, .5f * anim);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            windowRect = GUI.Window(99, windowRect, DrawWindow, "Mod Inspector");

            HandleResize();
        }

        // =========================
        void DrawWindow(int id)
        {
            scrollMain = GUILayout.BeginScrollView(scrollMain);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:");
            search = GUILayout.TextField(search);
            if (GUILayout.Button("Theme", GUILayout.Width(60)))
                themeIndex = (themeIndex + 1) % 3;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            scrollList = GUILayout.BeginScrollView(scrollList, GUILayout.Height(220));

            foreach (string p in plugins)
            {
                if (!string.IsNullOrEmpty(search) &&
                   !p.ToLower().Contains(search.ToLower()) &&
                   !lookup[p].Metadata.GUID.ToLower().Contains(search.ToLower()))
                    continue;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(p))
                    selected = lookup[p];

                DrawRisk(riskScores[p]);

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (selected != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Name: " + selected.Metadata.Name);
                GUILayout.Label("GUID: " + selected.Metadata.GUID);
                GUILayout.Label("Version: " + selected.Metadata.Version);
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // =========================
        void DrawRisk(int score)
        {
            Color c = Color.green;
            if (score > 30) c = Color.yellow;
            if (score > 60) c = Color.red;

            GUI.color = c;
            GUILayout.Label(score + "%", GUILayout.Width(40));
            GUI.color = Color.white;
        }

        // =========================
        void HandleResize()
        {
            Rect r = new Rect(windowRect.xMax - 16, windowRect.yMax - 16, 16, 16);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                resizing = true;
                resizeStart = Event.current.mousePosition;
            }

            if (resizing)
            {
                Vector2 diff = Event.current.mousePosition - resizeStart;
                windowRect.width += diff.x;
                windowRect.height += diff.y;
                resizeStart = Event.current.mousePosition;

                if (Event.current.type == EventType.MouseUp)
                    resizing = false;
            }
        }

        // =========================
        void ApplyTheme()
        {
            if (themeIndex == 0) GUI.backgroundColor = new Color(.15f, .15f, .15f);
            if (themeIndex == 1) GUI.backgroundColor = new Color(0, .2f, .2f);
            if (themeIndex == 2) GUI.backgroundColor = new Color(.8f, .8f, .8f);
        }
    }
}