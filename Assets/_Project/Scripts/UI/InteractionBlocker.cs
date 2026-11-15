using Room0317.Dialogue;
using Room0317.Inventory;
using Room0317.Story;

namespace Room0317.UI
{
    /// <summary>
    /// Shared guard that prevents world interactions or HUD clicks from triggering
    /// while an inspection modal, phone notification, dialogue box, menu, or story sequence is active or recently closed.
    /// </summary>
    public static class InteractionBlocker
    {
        public static bool IsBlocked(DialogueBox dialogueOverride = null)
        {
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return true;
            if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsShowing) return true;
            if (EndingScreen.Instance != null && EndingScreen.Instance.IsShowing) return true;

            var modal = InspectionModal.Instance;
            if (modal != null && (modal.IsOpen || modal.IsClosedRecently)) return true;

            var phone = PhoneNotificationUI.Instance;
            if (phone != null && (phone.IsShowing || phone.IsClosedRecently)) return true;

            var dialogue = dialogueOverride != null ? dialogueOverride : DialogueBox.ActiveInstance;
            if (dialogue != null && (dialogue.IsOpen || dialogue.IsClosedRecently)) return true;

            var story = StoryProgressionCoordinator.Instance;
            if (story != null && story.IsStorySequenceActive) return true;

            return false;
        }
    }
}
