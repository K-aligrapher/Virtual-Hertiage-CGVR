using System;
using UnityEngine;

namespace VirtualHeritage
{
    [Serializable] public class HeritageElement
    {
        public string name, type, material, period, description, historicalConfidence;
    }

    [Serializable] public class TimelineEntry
    {
        public string date, title, description;
    }

    [Serializable] public class TempleRecord
    {
        public string id, name, location, period, tradition, introduction, sourceUrl;
        public HeritageElement[] elements;
        public TimelineEntry[] timeline;
    }

    [Serializable] public class TempleCatalog
    {
        public TempleRecord[] temples;

        public static TempleCatalog Load()
        {
            var file = Resources.Load<TextAsset>("temples");
            if (!file) throw new InvalidOperationException("Local temple catalog is missing.");
            var catalog = JsonUtility.FromJson<TempleCatalog>(file.text);
            if (catalog?.temples == null || catalog.temples.Length != 3)
                throw new InvalidOperationException("The catalog must contain three temples.");
            foreach (var temple in catalog.temples)
                if (temple.elements == null || temple.elements.Length < 3 || temple.timeline == null || temple.timeline.Length < 3)
                    throw new InvalidOperationException("Incomplete temple record: " + temple.id);
            return catalog;
        }
    }
}
