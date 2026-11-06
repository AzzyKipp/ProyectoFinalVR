using TMPro;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Panel del contador (World Space) que se ubica sobre la mesa de los cajones.
    /// </summary>
    public class CountdownTimerUI : MonoBehaviour
    {
        public TMP_Text timeLabel;
        public Color normalColor = new Color(1f, 0.86f, 0.45f);
        public Color warningColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Segundos restantes a partir de los cuales el contador se pone rojo.")]
        public float warningThreshold = 60f;

        private string _last;

        public void SetTime(float seconds)
        {
            if (timeLabel == null) return;
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.CeilToInt(seconds);
            string text = $"{total / 60:00}:{total % 60:00}";
            if (text != _last)
            {
                timeLabel.text = text;
                _last = text;
            }

            Color color = seconds <= warningThreshold ? warningColor : normalColor;
            if (seconds > 0f && seconds <= 10f)
            {
                color = Color.Lerp(warningColor, Color.white, Mathf.PingPong(Time.time * 2f, 1f) * 0.6f);
            }
            timeLabel.color = color;
        }
    }
}
