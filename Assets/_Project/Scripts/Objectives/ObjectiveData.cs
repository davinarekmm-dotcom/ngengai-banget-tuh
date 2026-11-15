using System;
using UnityEngine;

namespace Room0317.Objectives
{
    /// <summary>
    /// Data structure representing a single narrative milestone or objective in ROOM 03:17.
    /// Tracks completion state, narrative description, player hints, and chronological time of event.
    /// </summary>
    [Serializable]
    public class ObjectiveData
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [SerializeField] private string hint;
        [SerializeField] private string timeString;
        [SerializeField] private bool isCompleted;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string Hint => hint;
        public string TimeString => timeString;
        public bool IsCompleted => isCompleted;

        public ObjectiveData(string id, string title, string description, string hint = "", string timeString = "03:12 AM")
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.hint = hint;
            this.timeString = timeString;
            this.isCompleted = false;
        }

        public void MarkComplete()
        {
            isCompleted = true;
        }

        public void Reset()
        {
            isCompleted = false;
        }
    }
}
