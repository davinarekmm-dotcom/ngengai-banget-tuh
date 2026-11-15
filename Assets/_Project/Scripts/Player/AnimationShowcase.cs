using UnityEngine;

namespace Room0317.Player
{
    /// <summary>
    /// Loops a single Animator state forever. Used in the test scene to display every Adrian animation side by side.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationShowcase : MonoBehaviour
    {
        [SerializeField] private string stateName = "Idle_Down";

        public string StateName
        {
            get => stateName;
            set => stateName = value;
        }

        private void Start()
        {
            GetComponent<Animator>().Play(stateName, 0, 0f);
        }
    }
}
