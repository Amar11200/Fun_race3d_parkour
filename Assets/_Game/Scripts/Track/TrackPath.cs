using System;
using System.Collections.Generic;
using UnityEngine;

namespace DashMasters
{
    public struct PathPoint
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
        public Vector3 right;
        public float distance;
    }

    public enum TrackZoneType
    {
        Normal,
        Ladder,
        Slider
    }

    [System.Serializable]
    public struct TrackZone
    {
        public float startDistance;
        public float endDistance;
        public TrackZoneType zoneType;
    }

    public class TrackPath : MonoBehaviour
    {
        public static TrackPath Instance { get; private set; }

        [SerializeField] private List<Vector3> waypoints = new List<Vector3>();
        [SerializeField] private List<TrackZone> zones = new List<TrackZone>();
        private List<PathPoint> sampledPoints = new List<PathPoint>();
        private List<Checkpoint> registeredCheckpoints = new List<Checkpoint>();

        public float TotalLength { get; private set; }
        public List<TrackZone> Zones => zones;

        public void ClearZones()
        {
            zones.Clear();
        }

        public void AddZone(float startDist, float endDist, TrackZoneType type)
        {
            zones.Add(new TrackZone { startDistance = startDist, endDistance = endDist, zoneType = type });
        }

        public bool IsLadder(float dist)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].zoneType == TrackZoneType.Ladder && dist >= zones[i].startDistance && dist <= zones[i].endDistance)
                    return true;
            }
            return false;
        }

        public bool IsSlider(float dist)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].zoneType == TrackZoneType.Slider && dist >= zones[i].startDistance && dist <= zones[i].endDistance)
                    return true;
            }
            return false;
        }

        public TrackZone? GetZoneAtDistance(float dist)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (dist >= zones[i].startDistance && dist <= zones[i].endDistance)
                    return zones[i];
            }
            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (waypoints.Count >= 2 && sampledPoints.Count == 0)
            {
                BakePath(waypoints);
            }
        }

        public void SetWaypoints(List<Vector3> points)
        {
            waypoints = new List<Vector3>(points);
            BakePath(waypoints);
        }

        public void BakePath(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return;

            sampledPoints.Clear();
            float cumulative = 0f;

            // Catmull-Rom or multi-segment spline sampling
            List<Vector3> smoothed = GenerateCatmullRomSpline(points, 12);

            for (int i = 0; i < smoothed.Count; i++)
            {
                Vector3 pos = smoothed[i];
                Vector3 forward = Vector3.forward;

                if (i < smoothed.Count - 1)
                {
                    forward = (smoothed[i + 1] - pos).normalized;
                }
                else if (i > 0)
                {
                    forward = (pos - smoothed[i - 1]).normalized;
                }

                if (forward == Vector3.zero) forward = Vector3.forward;

                Vector3 refUp = Vector3.up;
                // If path is vertical or near-vertical (climbing ladder), reference up must not be parallel to forward
                if (Mathf.Abs(Vector3.Dot(refUp, forward)) > 0.88f)
                {
                    refUp = (sampledPoints.Count > 0) ? -sampledPoints[sampledPoints.Count - 1].forward : Vector3.back;
                    refUp.y = 0f;
                    if (refUp.sqrMagnitude < 0.001f) refUp = Vector3.back;
                    refUp.Normalize();
                }

                Vector3 right = Vector3.Cross(refUp, forward).normalized;
                if (right == Vector3.zero) right = (sampledPoints.Count > 0) ? sampledPoints[sampledPoints.Count - 1].right : Vector3.right;
                Vector3 up = Vector3.Cross(forward, right).normalized;

                if (i > 0)
                {
                    cumulative += Vector3.Distance(smoothed[i], smoothed[i - 1]);
                }

                sampledPoints.Add(new PathPoint
                {
                    position = pos,
                    forward = forward,
                    up = up,
                    right = right,
                    distance = cumulative
                });
            }

            TotalLength = cumulative;
        }

        public PathPoint EvaluateDistance(float dist)
        {
            if (sampledPoints.Count == 0)
            {
                return new PathPoint
                {
                    position = transform.position + Vector3.forward * dist,
                    forward = Vector3.forward,
                    up = Vector3.up,
                    right = Vector3.right,
                    distance = dist
                };
            }

            dist = Mathf.Clamp(dist, 0f, TotalLength);

            // Binary search for closest segment
            int low = 0;
            int high = sampledPoints.Count - 1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (sampledPoints[mid].distance < dist)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int idx1 = Mathf.Clamp(high, 0, sampledPoints.Count - 1);
            int idx2 = Mathf.Clamp(idx1 + 1, 0, sampledPoints.Count - 1);

            if (idx1 == idx2) return sampledPoints[idx1];

            float d1 = sampledPoints[idx1].distance;
            float d2 = sampledPoints[idx2].distance;
            float t = (d2 > d1) ? (dist - d1) / (d2 - d1) : 0f;

            PathPoint p1 = sampledPoints[idx1];
            PathPoint p2 = sampledPoints[idx2];

            Vector3 pos = Vector3.Lerp(p1.position, p2.position, t);
            Vector3 fwd = Vector3.Slerp(p1.forward, p2.forward, t).normalized;
            Vector3 up = Vector3.Slerp(p1.up, p2.up, t).normalized;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            return new PathPoint
            {
                position = pos,
                forward = fwd,
                up = up,
                right = right,
                distance = dist
            };
        }

        public void RegisterCheckpoint(Checkpoint cp)
        {
            if (!registeredCheckpoints.Contains(cp))
            {
                registeredCheckpoints.Add(cp);
            }
        }

        public void ClearCheckpoints()
        {
            registeredCheckpoints.Clear();
        }

        public Checkpoint GetLatestCheckpoint(float racerDist)
        {
            Checkpoint best = null;
            float bestDist = -1f;

            foreach (var cp in registeredCheckpoints)
            {
                if (cp != null && cp.TrackDistance <= racerDist && cp.TrackDistance > bestDist)
                {
                    best = cp;
                    bestDist = cp.TrackDistance;
                }
            }

            return best;
        }

        private List<Vector3> GenerateCatmullRomSpline(List<Vector3> controlPoints, int pointsPerSegment)
        {
            List<Vector3> result = new List<Vector3>();
            if (controlPoints.Count < 2) return result;

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                Vector3 p0 = i == 0 ? controlPoints[i] : controlPoints[i - 1];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : p2;

                for (int j = 0; j < pointsPerSegment; j++)
                {
                    float t = (float)j / pointsPerSegment;
                    Vector3 pt = CalculateCatmullRomPoint(t, p0, p1, p2, p3);
                    result.Add(pt);
                }
            }

            result.Add(controlPoints[controlPoints.Count - 1]);
            return result;
        }

        private Vector3 CalculateCatmullRomPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2.0f * p1) +
                (-p0 + p2) * t +
                (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
            );
        }
    }
}
