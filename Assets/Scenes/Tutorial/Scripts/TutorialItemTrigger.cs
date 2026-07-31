using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// One entry per item you want to watch for. Either or both of beginStepId /
// completeStepId can be set: beginStepId reveals a step with autoStart = false
// (via TutorialManager.BeginStep), completeStepId finishes a CustomEvent step
// (via TutorialManager.CompleteCustomStep). You can use both on the same
// entry (e.g. the item both reveals step 4 and completes step 3), or spread
// them across separate entries.
[System.Serializable]
public class ItemTriggerEntry {
    [Tooltip("Item to watch for. Matches PlayerInventory.HasItem(itemID).")]
    public string itemID;

    [Tooltip("Optional — step id to reveal (TutorialManager.BeginStep). Leave blank to skip.")]
    public string beginStepId;

    [Tooltip("Optional — step id to complete (TutorialManager.CompleteCustomStep). Leave blank to skip.")]
    public string completeStepId;

    [System.NonSerialized] public bool firedBegin;
    [System.NonSerialized] public bool firedComplete;

    public bool IsDone =>
        (string.IsNullOrEmpty(beginStepId) || firedBegin) &&
        (string.IsNullOrEmpty(completeStepId) || firedComplete);
}

// Standalone — does NOT need to be on the player prefab. Put this on any
// scene object (e.g. next to TutorialManager). Supports multiple item
// watches in one component. Waits for the local player's PlayerObject to
// spawn, finds PlayerInventory on it, and reacts to
// PlayerInventory.OnSlotChanged rather than polling.
public class TutorialItemTrigger : MonoBehaviour {
    public List<ItemTriggerEntry> entries = new List<ItemTriggerEntry>();

    PlayerInventory inventory;

    void OnEnable() {
        StartCoroutine(WaitForLocalPlayer());
    }

    void OnDisable() {
        StopAllCoroutines();
        if (inventory != null)
            inventory.OnSlotChanged -= HandleSlotChanged;
    }

    IEnumerator WaitForLocalPlayer() {
        while (NetworkManager.Singleton == null
               || NetworkManager.Singleton.LocalClient == null
               || NetworkManager.Singleton.LocalClient.PlayerObject == null) {
            yield return null;
        }

        inventory = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerInventory>();
        if (inventory == null) {
            Debug.LogWarning($"TutorialItemTrigger '{name}' couldn't find PlayerInventory on the local player object.");
            yield break;
        }

        inventory.OnSlotChanged += HandleSlotChanged;

        // Cover items already in the inventory by the time this fires
        // (e.g. tutorial reached this point after the item was picked up).
        CheckAll();
    }

    void HandleSlotChanged(int slotIndex) => CheckAll();

    void CheckAll() {
        if (inventory == null) return;

        bool allDone = true;

        foreach (var entry in entries) {
            if (entry.IsDone) continue;

            if (inventory.HasItem(entry.itemID)) {
                if (!string.IsNullOrEmpty(entry.beginStepId) && !entry.firedBegin) {
                    entry.firedBegin = true;
                    if (TutorialManager.Instance != null)
                        StartCoroutine(TriggerBeginStep(entry.beginStepId));
                    else
                        Debug.LogWarning($"TutorialItemTrigger '{name}' fired but no TutorialManager.Instance found.");
                }

                if (!string.IsNullOrEmpty(entry.completeStepId) && !entry.firedComplete) {
                    entry.firedComplete = true;
                    if (TutorialManager.Instance != null)
                        StartCoroutine(TriggerCompleteStep(entry.completeStepId));
                    else
                        Debug.LogWarning($"TutorialItemTrigger '{name}' fired but no TutorialManager.Instance found.");
                }
            }

            if (!entry.IsDone) allDone = false;
        }

        // Nothing left to watch for — stop listening.
        if (allDone && inventory != null) {
            inventory.OnSlotChanged -= HandleSlotChanged;
        }
    }

    IEnumerator TriggerBeginStep(string beginStepId) {
        yield return new WaitForSeconds(2f);
        TutorialManager.Instance.BeginStep(beginStepId);
    }

    IEnumerator TriggerCompleteStep(string completeStepId) {
        yield return new WaitForSeconds(2f);
        TutorialManager.Instance.CompleteCustomStep(completeStepId);
    }
}