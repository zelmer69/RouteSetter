using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Pathfinding.Graph
{
    public enum trackClass
    {
        Station = 0,
        Road = 1
    }
    public class RailNode
    {
        public TrackID ID;
        public trackClass nodeclass;
        RailTrack Track;
        public RailTrack GetTrack() {  return Track; }
        public float length = 0;
        Track logictrack;
        List<CurveSegmentInfo> segments;
        public List<TrackID> Neighbours = new List<TrackID>();   // <-- initialize here
        public Vector3 position { get => Track.transform.position; }
        public bool occupied { get => logictrack.OccupiedLength > 0; }

        public RailNode(Track Logictrack)
        {
            ID = Logictrack.ID;
            nodeclass = (ID.IsGeneric() ? trackClass.Road : trackClass.Station);
            Track = Logictrack.RailTrack();
            logictrack = Logictrack;
            segments = SegmentHelper.GetSegmentInfos(Track.curve, 1, 300, false);

            foreach (Track track in logictrack.PossibleInTracks.ToArray())
            {
                Neighbours.Add(track.ID);
            }
            foreach (Track track in logictrack.PossibleOutTracks.ToArray())
            {
                if (Neighbours.Contains(track.ID)) continue;
                Neighbours.Add(track.ID);
            }

            length = (float)logictrack.length;
        }

        public float GetTravelTime()
        {
            float totaltraveltime = 0;
            foreach (CurveSegmentInfo segment in segments)
            {
                totaltraveltime += (segment.segmentLength / 1000) / segment.GetSpeed() * 3600;
            }
            return totaltraveltime;
        }
    }
}
