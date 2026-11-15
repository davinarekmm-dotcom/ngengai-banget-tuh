using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Room0317.Inventory
{
    /// <summary>
    /// Displays collected clues and evidence in a sleek, responsive HUD bar.
    /// Anchored cleanly in the bottom-right of the screen.
    /// Players can click any collected clue to re-examine its high-resolution
    /// inspection illustration and read its psychological details at any time.
    /// </summary>
    public class InventoryHUD : MonoBehaviour
    {
        public static InventoryHUD Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private Text headerLabel;
        [SerializeField] private Image backgroundTray;

        private readonly List<InventorySlotUI> activeSlots = new List<InventorySlotUI>();
        private readonly HashSet<string> knownItemIds = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (slotContainer == null || root == null)
            {
                BuildRuntimeUI();
            }
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Inventory Bar Root - anchored securely to bottom-right
            root = new GameObject("InventoryHUD_Bar", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = new Vector2(1f, 0f);
            rootRt.anchorMax = new Vector2(1f, 0f);
            rootRt.pivot = new Vector2(1f, 0f);
            rootRt.anchoredPosition = new Vector2(-20f, 20f);
            rootRt.sizeDelta = new Vector2(380f, 96f);

            // Frosted Dark Card Backing
            backgroundTray = root.GetComponent<Image>();
            backgroundTray.color = new Color(0.06f, 0.07f, 0.11f, 0.92f);

            // 2. Header label
            var headerGo = new GameObject("HeaderLabel", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(root.transform, false);
            headerLabel = headerGo.GetComponent<Text>();
            headerLabel.font = font;
            headerLabel.fontSize = 14;
            headerLabel.fontStyle = FontStyle.Bold;
            headerLabel.color = new Color(0.95f, 0.90f, 0.75f, 0.95f);
            headerLabel.alignment = TextAnchor.MiddleCenter;
            var headerRt = headerLabel.rectTransform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -6f);
            headerRt.sizeDelta = new Vector2(360f, 20f);

            // 3. Slot Container
            var containerGo = new GameObject("SlotContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(root.transform, false);
            slotContainer = (RectTransform)containerGo.transform;
            slotContainer.anchorMin = new Vector2(0f, 0f);
            slotContainer.anchorMax = new Vector2(1f, 1f);
            slotContainer.pivot = new Vector2(0.5f, 0.5f);
            slotContainer.offsetMin = new Vector2(10f, 8f);
            slotContainer.offsetMax = new Vector2(-10f, -28f);

            var hlg = containerGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Hidden initially until at least one clue is discovered
            root.SetActive(false);
        }

        private void Start()
        {
            InventoryManager.OnItemCollected += HandleItemCollected;
            InventoryManager.OnItemUsed += HandleItemUsed;
            RefreshHUD();
        }

        private void OnDestroy()
        {
            InventoryManager.OnItemCollected -= HandleItemCollected;
            InventoryManager.OnItemUsed -= HandleItemUsed;
        }

        private void HandleItemCollected(InventoryItem item)
        {
            RefreshHUD();
        }

        private void HandleItemUsed(InventoryItem item)
        {
            RefreshHUD();
        }

        public void RefreshHUD()
        {
            if (InventoryManager.Instance == null || slotContainer == null) return;

            var items = InventoryManager.Instance.Items;

            if (root != null)
            {
                // Visible whenever 1 or more evidence items are logged
                root.SetActive(items.Count > 0);
            }

            if (headerLabel != null)
            {
                headerLabel.text = items.Count > 0
                    ? $"✦ EVIDENCE & CLUES ({items.Count}) ✦"
                    : "";
            }

            // Remove existing slot GameObjects
            for (int i = slotContainer.childCount - 1; i >= 0; i--)
            {
                var child = slotContainer.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }
            activeSlots.Clear();

            // Create slot for each collected item
            foreach (var item in items)
            {
                if (item == null) continue;

                bool isNew = knownItemIds.Add(item.id);
                var slotGo = CreateSlotObject(item);
                if (slotGo != null)
                {
                    var slotUI = slotGo.GetComponent<InventorySlotUI>();
                    if (slotUI != null) activeSlots.Add(slotUI);
                    if (isNew)
                    {
                        StartCoroutine(AnimateSlotPopRoutine(slotGo.transform as RectTransform));
                    }
                }
            }
        }

        private IEnumerator AnimateSlotPopRoutine(RectTransform slotRt)
        {
            if (slotRt == null) yield break;
            float duration = 0.22f;
            float elapsed = 0f;
            slotRt.localScale = new Vector3(0.45f, 0.45f, 1f);

            while (elapsed < duration && slotRt != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.12f;
                float scale = Mathf.Lerp(0.45f, 1.0f, 1f - Mathf.Pow(1f - t, 3f)) + overshoot;
                slotRt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            if (slotRt != null) slotRt.localScale = Vector3.one;
        }

        private GameObject CreateSlotObject(InventoryItem item)
        {
            var go = new GameObject("Slot_" + item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(slotContainer, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(54f, 54f);

            var bgImg = go.GetComponent<Image>();
            bgImg.color = new Color(0.16f, 0.17f, 0.24f, 0.95f);

            // Icon child
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.08f, 0.08f);
            iconRt.anchorMax = new Vector2(0.92f, 0.92f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            var iconImg = iconGo.GetComponent<Image>();
            var spr = item.icon != null ? item.icon : item.inspectionSprite;
            if (spr == null)
            {
                spr = Room0317.Story.StoryProgressionCoordinator.ResolveClueSprite(item.id);
            }
            iconImg.sprite = spr;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var slotUI = go.AddComponent<InventorySlotUI>();
            slotUI.Initialize(item);

            return go;
        }
    }

    /// <summary>
    /// Helper component on an individual inventory slot.
    /// Clicking the slot re-opens InspectionModal to view the clue.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour
    {
        private InventoryItem boundItem;

        public void Initialize(InventoryItem item)
        {
            boundItem = item;
            var btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (Room0317.UI.InteractionBlocker.IsBlocked()) return;

            if (boundItem != null && InspectionModal.Instance != null)
            {
                InspectionModal.Instance.ShowItem(boundItem, null, null);
            }
        }
    }
}
