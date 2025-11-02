using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach this script to the Scroll View object.
/// It provides a public method to force the scroll rect to the bottom.
/// This is essential for chat boxes and other scrolling logs.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class AutoScroll : MonoBehaviour
{
    private ScrollRect scrollRect;

    void Awake()
    {
        // Get the ScrollRect component attached to this GameObject.
        scrollRect = GetComponent<ScrollRect>();
    }

    /// <summary>
    /// Call this method from your chat manager script whenever a new message is added.
    /// </summary>
    public void ScrollToBottom()
    {
        // Coroutines are used to wait for the UI to update before scrolling.
        // If we don't wait, it will scroll to the second-to-last message.
        StartCoroutine(ForceScrollDown());
    }

    private IEnumerator ForceScrollDown()
    {
        // Wait for the end of the frame to ensure the UI has been updated.
        yield return new WaitForEndOfFrame();

        // Force the scrollbar to the bottom.
        // For a vertical scrollbar, 0 is the bottom and 1 is the top.
        scrollRect.verticalNormalizedPosition = 0f;
        
        // You can also use this if you want a smoother scroll, but it's not instant.
        // Canvas.ForceUpdateCanvases();
        // scrollRect.verticalNormalizedPosition = 0f;
    }
}