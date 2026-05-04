using System;
using UnityEngine;

// Helper: Unity's JsonUtility cannot parse a top-level JSON array directly.
// This wrapper handles arrays and single objects.
public static class JsonHelper
{
    [Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }

    public static T[] FromJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new T[0];

        var s = json.TrimStart();
        
        // 1. Try to parse as a single object (starts with {)
        if (s.StartsWith("{"))
        {
            try
            {
                // Attempt to deserialize the single object (e.g., /twin/printers/ID)
                T single = JsonUtility.FromJson<T>(json);
                return new T[] { single };
            }
            catch
            {
                // fall through to wrapper parsing if it failed
            }
        }

        // 2. Wrap and parse as an array (starts with [)
        string newJson = "{\"array\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper?.array ?? new T[0];
    }
}