using System;
using UnityEngine;

namespace Room0317.UI
{
    /// <summary>
    /// Master horror clock for the apartment.
    /// Tracks the creeping progression from 03:12 AM toward the climax at 03:17 AM.
    /// Broadcasts events when minutes advance and when 03:17 strikes.
    /// </summary>
    public class ApartmentClock : MonoBehaviour
    {
        public static ApartmentClock Instance { get; private set; }

        [Header("Clock Settings")]
        [SerializeField] private int hour = 3;
        [SerializeField] private int minute = 12;

        public int Hour => hour;
        public int Minute => minute;
        public string FormattedTime => $"{hour:D2}:{minute:D2} AM";
        public bool Is0317 => hour == 3 && minute >= 17;

        public static event Action<int, int> OnTimeChanged;
        public static event Action OnTimeReached0317;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetTime(int newHour, int newMinute)
        {
            if (hour == newHour && minute == newMinute) return;

            hour = newHour;
            minute = newMinute;
            OnTimeChanged?.Invoke(hour, minute);

            if (hour == 3 && minute >= 17)
            {
                OnTimeReached0317?.Invoke();
            }
        }

        public void AdvanceMinute(int count = 1)
        {
            SetTime(hour, minute + count);
        }

        public void SetTimeString(string timeStr)
        {
            // e.g. "03:15 AM"
            if (string.IsNullOrEmpty(timeStr)) return;
            var parts = timeStr.Replace("AM", "").Replace("PM", "").Trim().Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
            {
                SetTime(h, m);
            }
        }
    }
}
