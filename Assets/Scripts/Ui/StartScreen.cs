using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PostureCheck
{
    /// <summary>
    /// Plain start screen: the app name and a "Start check" button. The mascot greets from its
    /// corner (driven by <see cref="FlowController"/>).
    /// </summary>
    public class StartScreen : MonoBehaviour
    {
        /// <summary>Shows the screen and returns when the button is tapped.</summary>
        public IEnumerator Run()
        {
            var config = AppConfig.Current;
            Ui.EnsureEventSystem();
            var canvas = Ui.Canvas("StartCanvas", transform, 20);
            var root = canvas.transform;
            Ui.Panel(root, "Background", new Vector2(0.5f, 0.5f), new Vector2(4000, 4000), new Color(0.12f, 0.12f, 0.12f));
            var title = Ui.Text(root, "AppName", new Vector2(0.5f, 0.72f), new Vector2(1000, 200), 110);
            title.text = config.appName;
            title.color = config.UiColor;

            bool started = false;
            var button = Ui.Button(root, "Start", config.Line("start.button"), new Vector2(0.5f, 0.45f), new Vector2(620, 170));
            button.onClick.AddListener(() => started = true);

            while (!started) yield return null;
            Destroy(canvas.gameObject);
        }
    }
}
