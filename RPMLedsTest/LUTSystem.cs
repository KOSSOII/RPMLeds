using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;


namespace RPMLeds
{
    public class FfbLutDebugGraph : MonoBehaviour
    {
        [Header("LUT")]
        public bool lutEnabled = true;
        public string lutFileName = "ffb_lut.lut"; // file inside your mod folder (or absolute path)
        public string lutFolderAbsolute = "";      // if empty: uses Application.dataPath as example

        [Header("Graph UI")]
        public bool showGraph = true;
        public Rect graphRect = new Rect(20, 20, 300, 220);
        public int sampleCount = 128;

        private FfbLut _lut = new FfbLut();
        private Texture2D _graphTex;
        private string _status = "Not loaded";

        void Start()
        {
            Reload();
        }

        public void Reload()
        {
            string folder = lutFolderAbsolute;
            if (string.IsNullOrEmpty(folder))
            {
                // Replace this with your mod folder path
                folder = Application.dataPath;
            }

            string path = Path.Combine(folder, lutFileName);

            bool ok = _lut.LoadFromFile(path);
            _status = ok ? "Loaded: " + path : "LUT load failed: " + _lut.LastError;

            BuildGraphTexture();
        }

        void OnGUI()
        {
            if (!showGraph)
            {
                CloseAndDestroy();
                return;
            }

            GUI.Box(graphRect, "FFB LUT");

            var inner = new Rect(graphRect.x + 10, graphRect.y + 25, graphRect.width - 20, graphRect.height - 55);

            if (_graphTex != null)
                GUI.DrawTexture(inner, _graphTex, ScaleMode.StretchToFill, false);

            GUI.Label(new Rect(graphRect.x + 10, graphRect.yMax - 28, graphRect.width - 20, 20), _status);

            if (GUI.Button(new Rect(graphRect.xMax - 170, graphRect.y + 5, 80, 18), "Reload"))
                Reload();

            bool newEnabled = GUI.Toggle(new Rect(graphRect.xMax - 85, graphRect.y + 7, 80, 18), lutEnabled, "Enable");
            lutEnabled = newEnabled;

            if (GUI.Button(new Rect(graphRect.xMax - 80, graphRect.yMax - 28, 70, 20), "Close"))
            {
                showGraph = false;
                CloseAndDestroy();
            }
        }
        private void CloseAndDestroy()
        {
            // destroy texture first
            if (_graphTex != null)
            {
                Destroy(_graphTex);
                _graphTex = null;
            }

            // destroy this GameObject safely in MSC
            UnityEngine.Object.Destroy(this.gameObject);
        }

        private void BuildGraphTexture()
        {
            int w = 256;
            int h = 256;

            if (_graphTex == null)
            {
                _graphTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _graphTex.wrapMode = TextureWrapMode.Clamp;
                _graphTex.filterMode = FilterMode.Point;
            }

            // background
            var bg = new Color(0.08f, 0.08f, 0.08f, 1f);
            var grid = new Color(0.14f, 0.14f, 0.14f, 1f);
            var linear = new Color(0.25f, 0.25f, 0.25f, 1f);
            var curve = _lut.IsValid ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.8f, 0.3f, 0.3f, 1f);

            // clear
            var pixels = _graphTex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
            _graphTex.SetPixels32(pixels);

            // grid lines (0%, 25%, 50%, 75%, 100%)
            for (int g = 0; g <= 4; g++)
            {
                int x = Mathf.RoundToInt(g * (w - 1) / 4f);
                int y = Mathf.RoundToInt(g * (h - 1) / 4f);
                DrawVLine(_graphTex, x, grid);
                DrawHLine(_graphTex, y, grid);
            }

            // linear reference y=x
            for (int i = 0; i < w; i++)
            {
                float x01 = i / (float)(w - 1);
                int y = Mathf.RoundToInt(x01 * (h - 1));
                SetPixelSafe(_graphTex, i, y, linear);
            }

            // LUT curve sampled
            if (_lut.IsValid)
            {
                int prevX = 0;
                int prevY = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    float x01 = i / (float)(sampleCount - 1);
                    float y01 = _lut.Evaluate01(x01);

                    int x = Mathf.RoundToInt(x01 * (w - 1));
                    int y = Mathf.RoundToInt(y01 * (h - 1));

                    DrawLine(_graphTex, prevX, prevY, x, y, curve);
                    prevX = x;
                    prevY = y;
                }
            }

            _graphTex.Apply(false, false);
        }

        // --- tiny drawing helpers (Texture2D) ---

        private static void SetPixelSafe(Texture2D t, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return;
            t.SetPixel(x, y, c);
        }

        private static void DrawVLine(Texture2D t, int x, Color c)
        {
            for (int y = 0; y < t.height; y++) SetPixelSafe(t, x, y, c);
        }

        private static void DrawHLine(Texture2D t, int y, Color c)
        {
            for (int x = 0; x < t.width; x++) SetPixelSafe(t, x, y, c);
        }

        // Bresenham
        private static void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, Color c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                SetPixelSafe(t, x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // expose to your force code
        public float ApplyLut(float limitedForce, float maxForce)
        {
            if (!lutEnabled) return limitedForce;
            return _lut.ApplyToForce(limitedForce, maxForce);
        }
    }


/// <summary>
/// Assetto Corsa style LUT:
/// each line: "x y" (0..1) e.g. "0.10 0.15"
/// Comments allowed (# or //). Blank lines allowed.
/// </summary>
    public sealed class FfbLut
    {
        public struct Point
        {
            public float x; // input  0..1
            public float y; // output 0..1
            public Point(float x, float y) { this.x = x; this.y = y; }
        }

        private readonly List<Point> _pts = new List<Point>(64);
        public List<Point> Points
        {
            get { return _pts; }
        }
        public bool IsValid => _pts.Count >= 2;

        public string LoadedPath { get; private set; }
        public string LastError { get; private set; }

        public bool LoadFromFile(string path)
        {
            LoadedPath = path;
            LastError = null;
            _pts.Clear();

            if (string.IsNullOrEmpty(path))
            {
                LastError = "Path is null/empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                LastError = "File not found: " + path;
                return false;
            }

            try
            {
                var text = File.ReadAllText(path);
                ParseText(text);
                FixAndValidate();
                if (!IsValid)
                {
                    LastError = "LUT invalid (need >=2 points, sorted by X, range 0..1).";
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                LastError = "Load failed: " + e.Message;
                return false;
            }
        }

        public void ParseText(string text)
        {
            _pts.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                // allow separators: space, tab, comma, semicolon
                var parts = line.Split(new[] { ' ', '\t', ',', ';','|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (TryParseFloat(parts[0], out var x) && TryParseFloat(parts[1], out var y))
                    _pts.Add(new Point(x, y));
            }
        }

        /// <summary>
        /// Evaluate LUT for x in [0..1], returns y in [0..1], linear interpolation.
        /// </summary>
        public float Evaluate01(float x)
        {
            if (!IsValid) return Mathf.Clamp01(x);

            x = Mathf.Clamp01(x);

            // edges
            if (x <= _pts[0].x) return Mathf.Clamp01(_pts[0].y);
            var last = _pts[_pts.Count - 1];
            if (x >= last.x) return Mathf.Clamp01(last.y);

            // find segment (small N => linear scan ok)
            for (int i = 0; i < _pts.Count - 1; i++)
            {
                var a = _pts[i];
                var b = _pts[i + 1];

                if (x >= a.x && x <= b.x)
                {
                    float dx = b.x - a.x;
                    float t = dx > 1e-6f ? (x - a.x) / dx : 0f;
                    return Mathf.Clamp01(Mathf.Lerp(a.y, b.y, t));
                }
            }

            return Mathf.Clamp01(x);
        }

        /// <summary>
        /// Apply LUT to a force expressed in your device units (same units as maxForce).
        /// - force is shaped by LUT magnitude.
        /// - sign is preserved.
        /// - returns shaped force still in device units (float).
        /// </summary>
        public float ApplyToForce(float force, float maxForce)
        {
            if (!IsValid) return force;
            if (maxForce <= 0f) return force;

            float sign = Mathf.Sign(force);
            float mag = Mathf.Abs(force);

            // normalize 0..1
            float x = Mathf.Clamp01(mag / maxForce);

            // LUT output 0..1
            float y = Evaluate01(x);

            // back to device units
            float shapedMag = y * maxForce;
            return sign * shapedMag;
        }

        /// <summary>
        /// Optional safety: clamp/sort/monotonic-fix so users cannot make a dangerous curve.
        /// - clamps x,y to 0..1
        /// - sorts by x
        /// - removes duplicate x
        /// - enforces y monotonic non-decreasing (prevents snap/oscillations)
        /// </summary>
        private void FixAndValidate()
        {
            if (_pts.Count < 2) return;

            // clamp
            for (int i = 0; i < _pts.Count; i++)
            {
                var p = _pts[i];
                p.x = Mathf.Clamp01(p.x);
                p.y = Mathf.Clamp01(p.y);
                _pts[i] = p;
            }

            // sort by x
            _pts.Sort((a, b) => a.x.CompareTo(b.x));

            // remove duplicate x (keep last)
            for (int i = _pts.Count - 2; i >= 0; i--)
            {
                if (Mathf.Abs(_pts[i].x - _pts[i + 1].x) < 1e-6f)
                    _pts.RemoveAt(i);
            }

            if (_pts.Count < 2) { _pts.Clear(); return; }

            // enforce monotonic y
            float prevY = _pts[0].y;
            for (int i = 1; i < _pts.Count; i++)
            {
                var p = _pts[i];
                if (p.y < prevY) p.y = prevY;
                prevY = p.y;
                _pts[i] = p;
            }
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
