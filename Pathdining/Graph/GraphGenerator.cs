using DV.Logic.Job;
using Pathfinding.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using static UnityModManagerNet.UnityModManager.Param;

namespace Pathfinding
{
    public static class DebugMarker
    {
        public static void SpawnBeacon(Vector3 worldPos, string label = "DEBUG")
        {
            // Tall, bright, unmissable pillar - visible from across the map
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = $"DebugBeacon_{label}";
            beacon.transform.position = worldPos + Vector3.up * 250f; // center of a 500m-tall pillar
            beacon.transform.localScale = new Vector3(5f, 5000f, 5f);  // thin, very tall

            // Remove collider so it doesn't interfere with anything
            var collider = beacon.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            // Bright, unlit, hard-to-miss material (magenta stands out against rail/terrain colors)
            var renderer = beacon.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = Color.magenta;
            renderer.material = mat;

            UnityEngine.Object.DontDestroyOnLoad(beacon);

            Debug.LogError($"[DebugMarker] Spawned beacon '{label}' at world position {worldPos}");
        }
    }
    public class GraphGenerator : MonoBehaviour
    {
        public Dictionary<TrackID,RailNode> Graph = new Dictionary<TrackID, RailNode>();

        public void GenerateGraph()
        {
            Debug.Log("[pathfinder][GraphGenerator] GenerateGraph start");

            if (RailTrackRegistry.RailTrackToLogicTrack == null)
            {
                Debug.LogError("[pathfinder][GraphGenerator] RailTrackRegistry.RailTrackToLogicTrack is null");
                return;
            }
            Debug.Log($"[pathfinder][GraphGenerator] RailTrackToLogicTrack count: {RailTrackRegistry.RailTrackToLogicTrack.Count}");

            int processed = 0, skippedNullValue = 0, skippedNullId = 0, errors = 0;

            foreach (var item in RailTrackRegistry.RailTrackToLogicTrack)
            {
                if (item.Value == null)
                {
                    skippedNullValue++;
                    continue;
                }

                RailNode node;
                try
                {
                    node = new RailNode(item.Value);
                }
                catch (System.Exception ex)
                {
                    errors++;
                    DebugMarker.SpawnBeacon(item.Key.transform.position);
                    Debug.LogError($"[pathfinder][GraphGenerator] RailNode constructor threw for item: {ex} building node for {item.Value.ID.FullID} at {item.Key.transform.position}");
                    continue;
                }

                if (node.ID == null)
                {
                    skippedNullId++;
                    continue;
                }

                if (Graph.ContainsKey(node.ID)) continue;
                Graph.Add(node.ID, node);
                processed++;
            }

            Debug.Log($"[pathfinder][GraphGenerator] GenerateGraph done. processed={processed}, " +
                $"skippedNullValue={skippedNullValue}, skippedNullId={skippedNullId}, errors={errors}, " +
                $"final Graph.Count={Graph.Count}");
        }

        void Start ()
        {
            GenerateGraph();

            Debug.Log($"[pathfinder] Graph generated with {Graph.Count} nodes");

        }
        void Update() {
            if (!PlayerManager.LastLoco)
                return;
            //List<CurveSegmentInfo> segmentInfos = SegmentHelper.GetSegmentInfos(PlayerManager.LastLoco.FrontBogie.track.curve, 1, 300, false);
           // Debug.Log($"there are: {segmentInfos.Count} segments on this track");
            
        }


    }
}
