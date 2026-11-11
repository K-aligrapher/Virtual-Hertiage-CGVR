using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VirtualHeritage
{
    // Headless reachability check: flood-fills where the walking capsule can go, using the rig's own collision rules.
    public static class WalkTest
    {
        const float Step = .36f, Radius = .25f, Eye = 1.7f, Cell = .1f;

        public static void Run()
        {
            foreach (int index in new[] { 0, 1 })
            {
                var g = new HeritageGeometry();
                var root = new GameObject("walk" + index);
                g.Temple(root.transform, index, false, null);
                HeritageAtmosphere.Garden(g, root.transform, index + 42);
                HeritageAtmosphere.Compound(g, root.transform, true);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.SetParent(root.transform); ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
                Physics.SyncTransforms();
                float sx = index == 1 ? -3.9f : 0f;
                var seen = new HashSet<Vector2Int>(); var q = new Queue<Vector2Int>();
                Vector2Int Key(float x, float z) => new Vector2Int(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell));
                var start = Key(sx, -3f); seen.Add(start); q.Enqueue(start);
                var feetY = new Dictionary<Vector2Int, float>(); feetY[start] = 0;
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); float cy = feetY[c];
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = c + d; if (seen.Contains(n)) continue;
                        var target = new Vector3(n.x * Cell, cy, n.y * Cell);
                        if (target.x < -16 || target.x > 16 || target.z < -12 || target.z > 20) continue;
                        if (!Physics.Raycast(target + Vector3.up * (Step + .05f), Vector3.down, out var hit, Step + 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        target.y = hit.point.y;
                        var bottom = target + Vector3.up * (Step + Radius); var top = target + Vector3.up * (Eye - Radius + .05f);
                        if (Physics.CheckCapsule(bottom, top, Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        seen.Add(n); feetY[n] = hit.point.y; q.Enqueue(n);
                    }
                }
                var sb = new StringBuilder("WALKTEST temple " + index + " reachable cells " + seen.Count + "\n");
                for (float z = 9f; z >= -3f; z -= .2f)
                {
                    for (float x = -8f; x <= 8f; x += .2f) sb.Append(seen.Contains(Key(x, z)) ? '.' : '#');
                    sb.Append("  z=" + z.ToString("0.0") + "\n");
                }
                foreach (float z in new[] { 1.5f, 3.5f, 5.0f, 6.0f, 7.0f }) sb.Append("WALKTEST x=" + sx + " z=" + z + " reachable=" + seen.Contains(Key(sx, z)) + "\n");

                foreach (var probe in new[] { new Vector3(-3.4f, 0f, -3.7f), new Vector3(-3.4f, 0f, -3.5f), new Vector3(-3.0f, 0f, -3.8f) })
                {
                    var cols = Physics.OverlapCapsule(probe + Vector3.up * (Step + Radius), probe + Vector3.up * (Eye - Radius + .05f), Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    var names = new StringBuilder(); foreach (var cl in cols) names.Append(cl.name + " " + cl.bounds + " | ");
                    Debug.Log("WALKTEST probe " + probe + " -> " + names);
                }

                for (float z = -1.8f; z <= .6f; z += .2f)
                {
                    var line = new StringBuilder("WALKTEST ray z=" + z.ToString("0.0") + ": ");
                    foreach (float fy in new[] { 0f, .24f, .48f, .72f, .26f, .56f })
                    {
                        var tgt = new Vector3(sx, fy, z);
                        bool blocked = Physics.CheckCapsule(tgt + Vector3.up * (Step + Radius), tgt + Vector3.up * (Eye - Radius + .05f), Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        bool hitOk = Physics.Raycast(tgt + Vector3.up * (Step + .05f), Vector3.down, out var h2, Step + 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        line.Append("[y" + fy + (blocked ? " BLK" : " free") + (hitOk ? " ground=" + h2.point.y.ToString("0.00") + ":" + h2.collider.name : " noground") + "] ");
                    }
                    Debug.Log(line);
                }

                {
                    var sb2 = new StringBuilder("WALKTEST spheres at x=" + sx + ": ");
                    foreach (float zz in new[] { -1.0f, -.4f, 0.5f })
                    {
                        sb2.Append(" | z=" + zz + " blockedY:");
                        for (float yy = 0; yy < 4f; yy += .1f) if (Physics.CheckSphere(new Vector3(sx, yy, zz), Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) sb2.Append(" " + yy.ToString("0.0"));
                    }
                    Debug.Log(sb2);
                }

                {
                    bool TryStepSim(ref Vector3 feet, Vector3 delta)
                    {
                        if (delta.sqrMagnitude < 1e-8f) return false;
                        var tg = feet + delta;
                        if (!Physics.Raycast(tg + Vector3.up * (Step + .05f), Vector3.down, out var hh, Step + 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                        tg.y = hh.point.y;
                        if (Physics.CheckCapsule(tg + Vector3.up * (Step + Radius), tg + Vector3.up * (Eye - Radius + .05f), Radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                        feet = tg; return true;
                    }
                    var sbw = new StringBuilder("WALKTEST frames: ");
                    foreach (float off in new[] { -.9f, -.5f, 0f, .5f, .9f })
                        foreach (float yawDeg in new[] { 0f, 20f, -20f })
                        {
                            var feet = new Vector3(sx + off, 0, -4f);
                            var dir = Quaternion.Euler(0, yawDeg, 0) * Vector3.forward;
                            for (int f = 0; f < 700; f++)
                            {
                                var d = dir * 2.6f * .016f;
                                if (!(TryStepSim(ref feet, d) || TryStepSim(ref feet, new Vector3(d.x, 0, 0)) || TryStepSim(ref feet, new Vector3(0, 0, d.z)))) break;
                            }
                            sbw.Append("[off" + off + " yaw" + yawDeg + " -> x" + feet.x.ToString("0.0") + " y" + feet.y.ToString("0.00") + " z" + feet.z.ToString("0.0") + "] ");
                        }
                    Debug.Log(sbw);
                }
                Debug.Log(sb.ToString());
                Object.DestroyImmediate(root);
            }
        }
    }
}
