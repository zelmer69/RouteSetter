using DV.Signs;
using PassengerJobs.Platforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Pathfinding.Graph
{
    //this is basically a copy from the code that assigns speed limits that i've found in source code. Original class is private, so have to do this
    //By combining that and info about station/yard i can get speed limit with position, t on curve, or by name of the track
    class CurveSegmentInfo
    {
        public BezierCurve curve;

        public float segmentLength;

        public float minRadius;

        public float bezierStartT;

        public float bezierEndT;

        public float assignedSpeed;
        public float GetSpeed()
        {
            if (!(assignedSpeed <= 0f))
            {
                return assignedSpeed;
            }

            return GetMaxSpeedForRadius();
        }

        public float GetMaxSpeedForRadius()
        {
            if (minRadius < 50f)
            {
                return 10f;
            }

            if (minRadius < 70f)
            {
                return 20f;
            }

            if (minRadius < 95f)
            {
                return 30f;
            }

            if (minRadius < 130f)
            {
                return 40f;
            }

            if (minRadius < 170f)
            {
                return 50f;
            }

            if (minRadius < 230f)
            {
                return 60f;
            }

            if (minRadius < 360f)
            {
                return 70f;
            }

            if (minRadius < 700f)
            {
                return 80f;
            }

            if (minRadius < 900f)
            {
                return 90f;
            }

            if (minRadius < 1200f)
            {
                return 100f;
            }

            return 120f;
        }
       
    }
    static class SegmentHelper //helper functions to generate segment infos 
    {
        public static List<CurveSegmentInfo> GetSegmentInfos(BezierCurve curve, float error, float minArcLength, bool reverse)
        {
            List<BezierArcApproximation.Arc> list = new List<BezierArcApproximation.Arc>();
            BezierArcApproximation.CalculateArcs(curve, error, list);
            if (reverse)
            {
                list.Reverse();
                for (int i = 0; i < list.Count; i++)
                {
                    BezierArcApproximation.Arc arc = list[i];
                    BezierArcApproximation.Arc value = list[i];
                    value.s = arc.e;
                    value.e = arc.s;
                    value.bezierStartT = arc.bezierEndT;
                    value.bezierEndT = arc.bezierStartT;
                    list[i] = value;
                }
            }

            List<List<float>> list2 = SignPlacerUtils.ChunkifyNumbers(list.Select((BezierArcApproximation.Arc a) => a.Length).ToList(), minArcLength);
            List<List<BezierArcApproximation.Arc>> list3 = new List<List<BezierArcApproximation.Arc>>();
            int num = 0;
            for (int j = 0; j < list2.Count; j++)
            {
                List<BezierArcApproximation.Arc> list4 = new List<BezierArcApproximation.Arc>();
                for (int k = 0; k < list2[j].Count; k++)
                {
                    list4.Add(list[num]);
                    num++;
                }

                list3.Add(list4);
            }

            List<CurveSegmentInfo> list5 = new List<CurveSegmentInfo>();
            for (int l = 0; l < list3.Count; l++)
            {
                List<BezierArcApproximation.Arc> list6 = list3[l];
                CurveSegmentInfo curveSegmentInfo = new CurveSegmentInfo
                {
                    curve = curve,
                    bezierStartT = list6.First().bezierStartT,
                    bezierEndT = list6.Last().bezierEndT,
                    minRadius = float.PositiveInfinity
                };
                foreach (BezierArcApproximation.Arc item in list6)
                {
                    curveSegmentInfo.segmentLength += item.Length;
                    if (item.r < curveSegmentInfo.minRadius)
                    {
                        curveSegmentInfo.minRadius = item.r;
                    }
                }

                if (l + 1 < list3.Count)
                {
                    float firstXMeters = curveSegmentInfo.GetSpeed() * 2f;
                    float minRadiusInFirstXMeters = GetMinRadiusInFirstXMeters(list3[l + 1], firstXMeters);
                    if (minRadiusInFirstXMeters < curveSegmentInfo.minRadius)
                    {
                        float minRadius = (Mathf.Clamp(curveSegmentInfo.minRadius, minRadiusInFirstXMeters, 1200f) + minRadiusInFirstXMeters) / 2f;
                        curveSegmentInfo.minRadius = minRadius;
                    }
                }

                list5.Add(curveSegmentInfo);
            }

            return list5;
        }
        static float GetMinRadiusInFirstXMeters(List<BezierArcApproximation.Arc> arcs, float firstXMeters)
        {
            if (arcs == null || arcs.Count == 0)
            {
                Debug.LogError("Must have at least one arc");
                return 0f;
            }

            if (firstXMeters <= 0f)
            {
                Debug.LogError("firstXMeters must be positive");
                return 0f;
            }

            float num = float.PositiveInfinity;
            float num2 = 0f;
            for (int i = 0; i < arcs.Count; i++)
            {
                if (!(num2 < firstXMeters))
                {
                    break;
                }

                BezierArcApproximation.Arc arc = arcs[i];
                num2 += arc.Length;
                if (arc.r < num)
                {
                    num = arc.r;
                }
            }

            return num;
        }
       
    }
}
