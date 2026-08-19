using DV.Logic.Job;
using DV.ThingTypes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using DV.Signs;
using HarmonyLib;


namespace RouteSetter.ETA
{
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
        public class BrakingDistanceEstimator : MonoBehaviour
        {

        private const float BRAKE_MASS_FACTOR = 8.695652E-05f;

        private const float UPDATE_INTERVAL = 1f;

        private float _timeSinceUpdate;
        private float _lastDistance;
        private float _lastDeceleration;
        private float _lastSpeed;
        private bool _hasData;
        private bool _savedstartingpoint = false;
        private bool _wheelslip = false;
        private Vector3 _startingPoint;
        private float _recordedbreakingdistance = 0f;

        private GameObject _currentLabel;
        private TextMeshPro _currentTmp;
        Vector3 GetlocationAlongTrack(Track track,Vector3 direction, float distance)
        {
            //track.ConnectO






            return new Vector3();
        }
        private static List<CurveSegmentInfo> GetSegmentInfos(BezierCurve curve, float error, float minArcLength, bool reverse)
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
        float GetClosestGlobalT(BezierCurve curve, Vector3 location, int totalSamples = 200)
        {
            float bestT = 0f;
            float bestDistSq = float.PositiveInfinity;

            for (int i = 0; i <= totalSamples; i++)
            {
                float t = i / (float)totalSamples; // normalized 0..1 across whole curve
                Vector3 point = curve.GetPointAt(t);
                float distSq = (point - location).sqrMagnitude;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                }
            }

            return bestT;
        }
        CurveSegmentInfo GetSegmentAtGlobalT(List<CurveSegmentInfo> infos, float globalT)
        {
            foreach (CurveSegmentInfo info in infos)
            {
                float lo = Mathf.Min(info.bezierStartT, info.bezierEndT);
                float hi = Mathf.Max(info.bezierStartT, info.bezierEndT);
                if (globalT >= lo && globalT <= hi)
                {
                    return info;
                }
            }
            return null;
        }
        float GetSpeedLimitForCar(TrainCar trainCar)
        {

            if (trainCar == null)
            {
                return 0f;
            }

            float speedLimit = 0f;

            if (trainCar.FrontBogie == null)
            {
                return speedLimit;
            }

            if (trainCar.FrontBogie.track == null)
            {
                return speedLimit;
            }

            BezierCurve curve = trainCar.FrontBogie.track.curve;

            if (curve == null)
            {
                return speedLimit;
            }

            float t = 0f;
            t = GetClosestGlobalT(curve, trainCar.transform.position);
            List<CurveSegmentInfo> curveSegments = new List<CurveSegmentInfo>();
            curveSegments = GetSegmentInfos(curve, 1, 300, false);

            CurveSegmentInfo currentsegment = null;
            currentsegment = GetSegmentAtGlobalT(curveSegments, t);

            if (currentsegment != null)
            {
                speedLimit = currentsegment.GetSpeed();
            }
            else
            {
                Debug.LogWarning($"GetSpeedLimitForCar: car '{trainCar.name}' at t={t} did NOT match any segment on track '{trainCar.FrontBogie.track.name}'. Segment count: {curveSegments.Count}");
            }

            return speedLimit;
        }
        float getspeedlimit(RailTrack track)
        {
            if (track == null || track.curve == null)
            {
                Debug.LogWarning($"getspeedlimit: track or curve is null for {track?.name ?? "unknown"}");
                return 0f;
            }

            List<CurveSegmentInfo> infos = GetSegmentInfos(track.curve, 1, 300, false);

            if (infos == null || infos.Count == 0)
            {
                Debug.LogWarning($"getspeedlimit: no segments for track '{track.name}', curve length {track.curve.length}");
                return 0f;
            }

            List<float> speedchange = new List<float>(infos.Count);
            foreach (CurveSegmentInfo info in infos)
            {
                speedchange.Add(info.GetSpeed());
            }

            return speedchange.Average();
        }
        void SpawnLabel(string message, Vector3 position)
        {
            if (_currentLabel == null)
            {
                _currentLabel = new GameObject("BrakingLabel");
                _currentTmp = _currentLabel.AddComponent<TextMeshPro>();

                _currentTmp.fontSize = 5;
                _currentTmp.alignment = TextAlignmentOptions.Center;
                _currentTmp.color = Color.white;

                // TMP needs a font asset assigned or it may render blank/invisible.
                // Try grabbing the default resource:
                _currentTmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            _currentTmp.text = message;
            _currentLabel.transform.position = position;

            // billboard toward camera
            if (Camera.main != null)
                _currentLabel.transform.rotation = Quaternion.LookRotation(_currentLabel.transform.position - Camera.main.transform.position);

        }
        private void Update()
        {

            _timeSinceUpdate += Time.deltaTime;
            if (_timeSinceUpdate < UPDATE_INTERVAL) return;
            _timeSinceUpdate = 0f;


            TrainCar playerCar = GetPlayerTrainCar();
            if (playerCar == null)
            {
                _hasData = false;
                return;
            }
            _wheelslip = playerCar.adhesionController.wheelSlide != 0;
            List<TrainCar> consist = GetCoupledConsist(playerCar);
            if (consist == null || consist.Count == 0)
            {
                _hasData = false;
                return;
            }

            float speed = GetConsistSpeed(consist);
            float deceleration = GetConsistDeceleration(consist);

            _lastSpeed = speed;
            _lastDeceleration = deceleration;
            _lastDistance = EstimateBrakingDistance(speed, deceleration);
            if (speed > 0 && playerCar.brakeSystem.brakingFactor > 0)//once we start to apply break while on the move
            {
                if (!_savedstartingpoint)
                    _startingPoint = playerCar.transform.position;
                _savedstartingpoint = true;
                _recordedbreakingdistance = Vector3.Distance(playerCar.transform.position, _startingPoint);
                Vector3 textlocation = playerCar.transform.forward * _lastDistance + playerCar.transform.position;
                SpawnLabel("you'll stop here", textlocation);
               // Debug.Log($"Spawning text at {textlocation}, distance used: {_lastDistance}");

            }
            else
            {
                _savedstartingpoint = false;
            }

            _hasData = true;
        }

        /// <summary>
        /// Core formula: d = v^2 / (2a). Returns 0 if deceleration is ~0 (e.g. brakes fully released
        /// and no rolling resistance) to avoid divide-by-zero / infinity.
        /// </summary>
        public static float EstimateBrakingDistance(float speed, float deceleration)
        {
            if (deceleration <= 0.0001f) return float.PositiveInfinity;
            return (speed * speed) / (2f * deceleration);
        }


        public static float GetTraincarCurrentGrade(TrainCar car)
        {
            Vector3 vector = car.transform.forward;
            float num = vector.y * 100f / Mathf.Sqrt(vector.x * vector.x + vector.z * vector.z);
            return num;
        }

        /// <summary>
        /// Per-car deceleration contribution from brakes + rolling resistance, mass-independent.
        /// </summary>

        public static float GetCarDeceleration(TrainCar car, float gravityMagnitude)
        {
            TrainCarType_v2 type = car.carLivery.parentType;

            float totalBrakingForce = 0f;
            foreach (Bogie bogie in car.Bogies)
                totalBrakingForce += bogie.brakingForce;

            float mass = car.massController.TotalMass;
            float aBrake = (mass > 0f) ? totalBrakingForce / mass : 0f;
            float aRolling = type.RollingResistanceCoef * gravityMagnitude;

            // Percent grade -> fraction (divide by 100)
            float gradeFraction = GetTraincarCurrentGrade(car) / 100f;

            // Correct for direction of actual travel vs car.transform.forward
            float travelSign = (car.rb.velocity.sqrMagnitude > 0.01f)
                ? Mathf.Sign(Vector3.Dot(car.rb.velocity, car.transform.forward))
                : 1f;

            float aGrade = travelSign * gradeFraction * gravityMagnitude;

            return aBrake + aRolling + aGrade;
        }

        /// <summary>
        /// Mass-weighted average deceleration across all cars in the consist.
        /// Equivalent to (sum of braking+resistance forces) / (total mass).
        /// </summary>
        public static float GetConsistDeceleration(List<TrainCar> consist)
        {
            float g = 9.81f;

            float totalMass = 0f;
            float totalWeightedDecel = 0f;

            foreach (TrainCar car in consist)
            {

                float mass = car.massController.TotalMass;
                
                float a = GetCarDeceleration(car, g);

                totalMass += mass;
                totalWeightedDecel += mass * a;
            }

            if (totalMass <= 0f) return 0f;
            return totalWeightedDecel / totalMass;


        }

        /// <summary>
        /// Current consist speed. All coupled, non-derailed cars should share the same speed
        /// along the track, so just reading the lead car's rigidbody speed is normally sufficient.
        /// </summary>
        public static float GetConsistSpeed(List<TrainCar> consist)
        {
            TrainCar car = consist.FirstOrDefault(c => c.rb != null);
            if (car == null) return 0f;
            return car.rb.velocity.magnitude;
           
        }

        private static TrainCar GetPlayerTrainCar()
        {

            return PlayerManager.LastLoco;
        }

        private static List<TrainCar> GetCoupledConsist(TrainCar startCar)
        {

            var result = new List<TrainCar> { startCar };
            TrainCar current = startCar;
            while (current.frontCoupler?.coupledTo != null)
            {
                current = current.frontCoupler.coupledTo.train;
                result.Add(current);
            }
            current = startCar;
            while (current.rearCoupler?.coupledTo != null)
            {
                current = current.rearCoupler.coupledTo.train;
                result.Add(current);
            }
            return result;


        }

        // ------------------------------------------------------------------
        // Simple on-screen HUD. Replace with your mod's UI system if you have one.
        // ------------------------------------------------------------------
        private void OnGUI()
        {
            return;
            if (!_hasData) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Speed: {_lastSpeed * 3.6f:F1} km/h");
            sb.AppendLine($"Deceleration: {_lastDeceleration:F3} m/s^2");
            sb.AppendLine(float.IsInfinity(_lastDistance)
                ? "Braking distance: N/A (no braking/resistance)" +
                $" speed limit on current track {GetSpeedLimitForCar(PlayerManager.LastLoco)}"
                : $" Estimated Braking distance: {_lastDistance:F1} m" +
                $" Recorded breaking distance {_recordedbreakingdistance}" +
                $" speed limit on current track {GetSpeedLimitForCar(PlayerManager.LastLoco)}");

            RouteSetter.RouteSetterDebug.Log(sb.ToString());
        }
    }

}
