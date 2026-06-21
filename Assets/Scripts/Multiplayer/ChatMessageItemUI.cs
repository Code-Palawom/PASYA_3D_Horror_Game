using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageItemUI : MonoBehaviour {
    [Header("Message")]
    [SerializeField] Image messageRoot;
    [SerializeField] TMP_Text messageContentLabel;
    [SerializeField] Color normalColor;
    [SerializeField] Color systemColor;

    public void Setup(ChatMessage msg) {
        bool isSystem = msg.SenderId == ulong.MaxValue;

        messageContentLabel.text = isSystem ? $"<i><b>[System]</b>: {msg.Content}</i>" : $"<b>{msg.SenderName}</b>: {msg.Content}";
        if (isSystem) {
            messageRoot.color = systemColor;
        } else {
            messageRoot.color = normalColor;
        }
    }
}