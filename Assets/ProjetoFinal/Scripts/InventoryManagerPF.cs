using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryManagerPF : MonoBehaviour {
    public static InventoryManagerPF Instance;

    private Dictionary<BlockPF.BlockType, int> items = new Dictionary<BlockPF.BlockType, int>();
    private BlockPF.BlockType? highlightedType = null;

    public TMP_Text inventoryText;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddBlock(BlockPF.BlockType type, int amount = 1) {
        if (!items.ContainsKey(type)) items[type] = 0;
        items[type] += amount;
        UpdateUI();
    }

    public bool RemoveBlock(BlockPF.BlockType type, int amount = 1) {
        if (!items.ContainsKey(type) || items[type] < amount) return false;
        items[type] -= amount;
        UpdateUI();
        return true;
    }

    public int Count(BlockPF.BlockType type) {
        return items.ContainsKey(type) ? items[type] : 0;
    }

    public void SetHighlight(BlockPF.BlockType type) {
        highlightedType = type;
        UpdateUI();
    }

    void UpdateUI() {
        string text = "";
        foreach (var entry in items) {
            if (entry.Value <= 0) continue;
            if (highlightedType.HasValue && entry.Key == highlightedType.Value)
                text += $"<color=red>{entry.Key}: {entry.Value}</color>\n";
            else
                text += $"{entry.Key}: {entry.Value}\n";
        }
        if (inventoryText != null) inventoryText.text = text;
    }
}