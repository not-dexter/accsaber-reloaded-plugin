using AccSaber.API;
using AccSaber.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AccSaber.Counter
{
    internal class APCalc
    {
        private List<Vector2> PointList = null!;
        private float scale, shift;

        private float weight_x0, weight_k, weight_top;

        public APCalc()
        {
            Task.Run(Load);
        }
        private async Task Load()
        {
            AccSaberCurve? curve;
            try
            {
                curve = await APIHandler.CallAPI_Json<AccSaberCurve>(HelpfulPaths.APAPI_CURVE_AP, AccsaberAPI.Throttler);

                if (curve is not null && curve.Points is not null && curve.Scale is not null && curve.Shift is not null)
                {
                    PointList = curve.Points;
                    scale = curve.Scale.Value;
                    shift = curve.Shift.Value;

                    if (PointList[0].x < PointList[1].x)
                        PointList.Reverse();
                }
                else
                    throw new Exception("There was an issue parsing the ap curve!");
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }

            try
            {
                curve = await APIHandler.CallAPI_Json<AccSaberCurve>(HelpfulPaths.APAPI_CURVE_WEIGHT, AccsaberAPI.Throttler);

                if (curve is not null && curve.XName is not null && curve.XVal is not null && curve.YName is not null && curve.YVal is not null && curve.ZName is not null && curve.ZVal is not null)
                {
                    float x1 = float.MinValue, y1 = float.MinValue, k = float.MinValue;
                    IEnumerable<(string name, float val)> vals = [(curve.XName, curve.XVal.Value), (curve.YName, curve.YVal.Value), (curve.ZName, curve.ZVal.Value)];

                    foreach (var val in vals)
                        switch (val.name)
                        {
                            case "x1":
                                x1 = val.val;
                                break;
                            case "y1":
                                y1 = val.val;
                                break;
                            case "k":
                                k = val.val;
                                break;
                        }

                    if (x1 == float.MinValue || y1 == float.MinValue || k == float.MinValue)
                        throw new Exception("Weight values were not all found!");

                    weight_x0 = -Mathf.Log((1 - y1) / (y1 * Mathf.Exp(k * x1) - 1)) / k;
                    weight_top = 1 + Mathf.Exp(-k * weight_x0);
                    weight_k = k;
                }
                else
                    throw new Exception("There was an issue parsing the weighted curve!");
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
            }
        }
        public float GetAp(float acc, float complexity) => GetCurve(acc) * (complexity - shift) * scale;
        public float GetAccDeflated(float deflatedPp, float complexity, int precision = -1)
        {
            if (deflatedPp > GetAp(1.0f, complexity)) return precision < 0 ? 1.0f : 100.0f;
            float outp = InvertCurve(deflatedPp / (complexity - shift) * scale);
            return precision < 0 ? outp : (float)Math.Round(outp * 100.0f, precision);
        }

        public float GetWeight(int rank)
        { // rank is zero-indexed, meaning a player's top play is rank 0
            float bottom = 1 + Mathf.Exp(weight_k * (rank - weight_x0));
            return weight_top / bottom;
        }

        public float GetCurve(float acc) => GetCurve(acc, PointList);
        public float InvertCurve(double curveOutput) => GetInvertCurve(curveOutput, PointList);
        public static float GetCurve(float acc, List<Vector2> curve)
        {
            int i = 1;
            while (i < curve.Count && curve[i].x > acc) i++;
            double middle_dis = (acc - curve[i - 1].x) / (curve[i].x - curve[i - 1].x);
            return (float)(curve[i - 1].y + middle_dis * (curve[i].y - curve[i - 1].y));
        }
        public static float GetInvertCurve(double curveOutput, List<Vector2> curve)
        {
            int i = 1;
            while (i < curve.Count && curve[i].y > curveOutput) i++;
            double middle_dis = (curveOutput - curve[i - 1].y) / (curve[i].y - curve[i - 1].y);
            return (float)(curve[i - 1].x + middle_dis * (curve[i].x - curve[i - 1].x));
        }
    }
}
