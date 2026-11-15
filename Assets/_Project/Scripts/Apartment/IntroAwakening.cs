using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Room0317.Audio;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.Apartment
{
    /// <summary>
    /// Cold-open awakening sequence for ROOM 03:17.
    /// Provides an atmospheric start from pitch-black darkness, eye-blink fade-in,
    /// and Adrian's initial realization monologue before handing control to the player.
    /// </summary>
    public class IntroAwakening : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool skipIntro = true;
        [SerializeField] private float initialBlackoutDuration = 1.0f;

        public bool SkipIntro { get => skipIntro; set => skipIntro = value; }

        private ScreenFader screenFader;
        private DialogueBox dialogueBox;
        private AdrianController player;

        private void Start()
        {
            screenFader = FindFirstObjectByType<ScreenFader>();
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            player = FindFirstObjectByType<AdrianController>();

            if (!skipIntro)
            {
                StartCoroutine(AwakeningSequenceRoutine());
            }
        }

        private IEnumerator AwakeningSequenceRoutine()
        {
            if (player != null)
            {
                player.SetCanMove(false);
            }

            // Ensure screen starts completely black
            if (screenFader != null)
            {
                screenFader.SetInstantBlack();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0.4f);
            }

            yield return new WaitForSeconds(initialBlackoutDuration);

            // First blink open
            if (screenFader != null)
            {
                yield return screenFader.FadeIn(0.6f);
                yield return new WaitForSeconds(0.15f);
                yield return screenFader.FadeOut(0.2f);
                yield return new WaitForSeconds(0.1f);
                yield return screenFader.FadeIn(0.8f);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0.1f);
            }

            // Awakening monologue
            if (dialogueBox != null)
            {
                bool dialogueFinished = false;
                Action onFinished = null;
                onFinished = () =>
                {
                    dialogueFinished = true;
                    dialogueBox.OnDialogueFinished -= onFinished;
                };
                dialogueBox.OnDialogueFinished += onFinished;

                dialogueBox.StartDialogue(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Exhausted", "03:12 AM... I wake up. Cold air fills my lungs."),
                    new DialogueLine("Adrian", "Neutral", "The alarm wasn't set, but I woke up again at this exact hour. I should check the bedside clock.")
                });

                while (!dialogueFinished && dialogueBox.IsOpen)
                {
                    yield return null;
                }
            }

            if (player != null)
            {
                player.SetCanMove(true);
            }
        }
    }
}
