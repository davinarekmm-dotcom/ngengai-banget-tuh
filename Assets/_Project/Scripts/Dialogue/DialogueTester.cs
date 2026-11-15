using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Player;

namespace Room0317.Dialogue
{
    /// <summary>
    /// Test-scene driver: plays a sample conversation on start (cycling through every portrait expression)
    /// and lets the player re-open it with T. Movement is locked while the dialogue box is open.
    /// </summary>
    public class DialogueTester : MonoBehaviour
    {
        [SerializeField] private DialogueBox dialogueBox;
        [SerializeField] private AdrianController adrian;
        [SerializeField] private bool playOnStart = true;

        [SerializeField]
        private List<DialogueLine> lines = new List<DialogueLine>
        {
            new DialogueLine("Adrian", "Neutral", "03:17 again... Every single night, the same minute. My phone just buzzed, but there's no sender."),
            new DialogueLine("Adrian", "Exhausted", "I haven't slept properly in weeks. Maybe I'm just tired. Maybe none of this is real."),
            new DialogueLine("Adrian", "Scared", "Wait... that chair wasn't there a second ago. Somebody moved it. Somebody is in here."),
            new DialogueLine("Adrian", "Nervous", "Okay. Breathe. Use WASD or the arrow keys to look around. Press T if you want to hear me panic again."),
        };

        private void Start()
        {
            if (dialogueBox != null) dialogueBox.OnDialogueFinished += HandleFinished;
            if (playOnStart) Play();
        }

        private void OnDestroy()
        {
            if (dialogueBox != null) dialogueBox.OnDialogueFinished -= HandleFinished;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame && dialogueBox != null && !dialogueBox.IsOpen)
                Play();
        }

        private void Play()
        {
            if (adrian != null) adrian.CanMove = false;
            dialogueBox.StartDialogue(lines);
        }

        private void HandleFinished()
        {
            if (adrian != null) adrian.CanMove = true;
        }
    }
}
