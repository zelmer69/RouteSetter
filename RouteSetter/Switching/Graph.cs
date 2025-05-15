using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoPilot
{
    public class TrackNode
    {
        public string id;
        public Vector3 position;
        public RailTrack track;
        public Junction junction;
        public List<string> neighbors;

        public TrackNode(string id, Vector3 position)
        {
            this.id = id;
            this.position = position;
            this.neighbors = new List<string>();
        }
    }
}
