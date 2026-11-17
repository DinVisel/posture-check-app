using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// Builds the plain placeholder UI in code (canvases, text, buttons), so screens need no
    /// prefabs until the design arrives. Sizes are in a 1080×1920 reference layout.
    /// </summary>
    public static class Ui
    {
        static Font s_Font;
        static Font Font => s_Font != null ? s_Font : s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Text box anchored to a point of its parent (anchor 0..1), centred on it.</summary>
        public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            Place(text.rectTransform, anchor, size);
            return text;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, anchor, size);
            return image;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.62f, 0.62f, 0.62f); // config "ui" colour, until step 1.8 reads it
            Place(image.rectTransform, anchor, size);
            var text = Text(go.transform, "Label", new Vector2(0.5f, 0.5f), size, 48);
            text.text = label;
            text.color = Color.black;
            return go.GetComponent<Button>();
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        /// <summary>Buttons need an EventSystem; creates one (Input System module) if the scene has none.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
