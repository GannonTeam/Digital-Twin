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
        if (s.StartsWith("{"))
        {
            try
            {
                T single = JsonUtility.FromJson<T>(json);
                return new T[] { single };
            }
            catch
            {
                // fall through to wrapper parsing
            }
        }

        string newJson = "{\"array\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper?.array ?? new T[0];
    }
}