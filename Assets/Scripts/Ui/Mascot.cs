using UnityEngine;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// The mascot: an image (config "assets.mascotIdle") in the bottom corner with a speech bubble. Phase 1 placeholder:
    /// no voice and no animation, just the image and the text line for the current state.
    /// </summary>
    public class Mascot : MonoBehaviour
    {
        Canvas m_Canvas;
        RawImage m_Image;
        Image m_Bubble;
        Text m_Text;
        string m_Line;

        void Awake()
        {
            m_Canvas = Ui.Canvas("MascotCanvas", transform, 30);
            var root = m_Canvas.transform;

            var imgGo = new GameObject("MascotImage", typeof(RawImage));
            imgGo.transform.SetParent(root, false);
            m_Image = imgGo.GetComponent<RawImage>();
            m_Image.texture = AppConfig.LoadContentImage(AppConfig.Current.assets.mascotIdle);
            m_Image.raycastTarget = false;
            var rt = m_Image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(30, 30);
            rt.sizeDelta = new Vector2(260, 260);

            m_Bubble = Ui.Panel(root, "Bubble", Vector2.zero, new Vector2(700, 200), new Color(1f, 1f, 1f, 0.92f));
            var b = m_Bubble.rectTransform;
            b.pivot = Vector2.zero;
            b.anchoredPosition = new Vector2(310, 120);
            m_Text = Ui.Text(b, "Line", new Vector2(0.5f, 0.5f), new Vector2(660, 180), 50);
            m_Text.color = Color.black;

            Say(null);
        }

        /// <summary>Shows a line in the speech bubble; null or empty hides the bubble.</summary>
        public void Say(string line)
        {
            if (line == m_Line) return;
            m_Line = line;
            bool show = !string.IsNullOrEmpty(line);
            m_Bubble.gameObject.SetActive(show);
            if (show) m_Text.text = line;
        }

        /// <summary>Shows or hides the whole mascot (image and bubble).</summary>
        public void SetVisible(bool visible) => m_Canvas.gameObject.SetActive(visible);
    }
}
