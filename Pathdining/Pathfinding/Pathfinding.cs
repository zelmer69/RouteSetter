using DV.Logic.Job;
using HarmonyLib;
using Pathfinding.Graph;
using Pathfinding.Switching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Pathfinding
{
        public class Pathnode {
            public RailNode RailNode { get; }
            public float cost = 0;
            public float g = 0;
            public float h = 0;
            public Pathnode CameFrom { get; }
            public Pathnode(RailNode node,Pathnode from,RailNode end) {
                RailNode = node;
                CameFrom = from;
                if (end != null&& from !=null)
                {
                    g = RailNode.length + from.g;
                    h = Vector3.Distance(node.position, end.position);
                    cost = g + h;
                }
            }
    
        }
        public class path
        {
            public float TravelTime;//in seconds
            public float AverageSpeed;// in KM/h
            public RailNode[] Nodes;
            public float length;
            public path()
            {
                TravelTime = 0;
                AverageSpeed = 0;
                length = 0;
                Nodes = new RailNode[0];
            }
            public path(RailNode[] Path) {
                Nodes = Path;
        
        
            }
        }
        public static class Pathfiinder
        {

            static bool SwitchAlongPath(path path)
            {
                if(path == null) return false;
            RailNode[] nodes = path.Nodes;
            if(nodes.Length==0) return false;
           
            for(int i = 0; i == nodes.Length; i++)
            {
                RailNode node = nodes[i];
                JunctionHandler.TrySwitchJunctionToward(node.GetTrack(), nodes[i + 1].GetTrack());
            }
            return true;
            }
        public static bool FindPathAndSwitchJunctions(TrackID Start, TrackID End)
        {


           path path =  FindPath(Start, End, GraphHelper.GetGraph());
            return SwitchAlongPath(path);
        }
            static Pathnode GetBestPathnode(List<Pathnode> nodes)
            {
                Pathnode currentbest= null;
                float bestcost = float.MaxValue;
                foreach (Pathnode node in nodes) {
                    //if (currentbest == null) continue;
                    if (bestcost > node.cost) { 
                    bestcost = node.cost;
                    currentbest = node;
                    }
                
                }



                return currentbest;
            }
            static bool EvaluateNode(Pathnode node)
            {
                if (node == null) return false;
                if (node.RailNode.occupied) return false;
                if (node.RailNode.Neighbours.Count==1)return false;//dead end is likley to have 1 other neighbour
            
                return true;

            }
             static path reconstruct(Pathnode finalNode)
            {
                path path = new path();

                if (finalNode == null) return path;
                Pathnode currentnode = finalNode;
                Pathnode[] pathinreverse=new Pathnode[0];//can c# extend the array automatically? 
                while (currentnode != null)
                {
                    pathinreverse= pathinreverse.AddToArray(currentnode);
                    currentnode = currentnode.CameFrom;
                
                }

                pathinreverse.Reverse();
                RailNode[] ActualPath = new RailNode[0];
                float traveltime = 0;
                float travelditance = 0;

                foreach(Pathnode pathnode in pathinreverse)
                {
                    traveltime+= pathnode.RailNode.GetTravelTime();
                    travelditance += pathnode.RailNode.length;
                    ActualPath= ActualPath.AddToArray(pathnode.RailNode);
                }

            Debug.Log($"reconstructed path length is{path.Nodes.Length}, pathinreverese length {pathinreverse.Length}");
                path = new path(ActualPath);
                path.length = travelditance;
                path.TravelTime = traveltime;
                return path;
            }


        

            public static path FindPath(TrackID Start, TrackID End,Dictionary<TrackID,RailNode> RailGraph)
        {   
            RailNode start= RailGraph[Start];
            RailNode end= RailGraph[End];
            Debug.Log($"[Pathfinder][FindPath]start track in graph{start.ID.FullID},end track in graph {end.ID.FullID}");
            return FindPath(start, end,RailGraph);
        }
            public static path FindPath(RailNode start, RailNode end, Dictionary<TrackID, RailNode> RailGraph) {

                Debug.Log($"[Pathfinder][FindPath]start track in graph{start.ID.FullID},end track in graph {end.ID.FullID}");
                path path = new path();
                List<Pathnode>openset = new List<Pathnode>();
                List<RailNode>visited = new List<RailNode>();
                if (start == end) return path;
                if (start==null)return path;
                if(end==null)return path;
                openset.Add(new Pathnode(start,null,end));
                Pathnode currentBest= null;
                Pathnode finalpathnode= null;
                while (openset.Count > 0) {
                
                    if(currentBest != null)openset.Remove(currentBest);
                    currentBest= GetBestPathnode(openset);
                
                    visited.Add(currentBest.RailNode);
                    List<TrackID> neighbors = currentBest.RailNode.Neighbours;
                    foreach (TrackID node in neighbors)//convert all neigbours into pathnodes. process of conversion will calculate the cost
                    {
                        Pathnode currentnode = new Pathnode(RailGraph[node], currentBest, end);

                        if (currentnode == null) continue;
                        if (currentnode.RailNode.ID == end.ID)
                        {
                            currentBest = currentnode;
                            break;

                        }
                        if (!EvaluateNode(currentnode)) continue;//discard nodes that can't be used
                        if (visited.Contains(currentnode.RailNode)) continue;
                        openset.Add(currentnode);
                    }
                    if (currentBest.RailNode.ID == end.ID) {
                        finalpathnode = currentBest;
                        break;
                    
                    }

                }
            Debug.Log($"Final path node is{finalpathnode.RailNode.ID.FullID} reconstructing");
            path = reconstruct(finalpathnode);
            
           
            
            return path;
            }
        }

}
