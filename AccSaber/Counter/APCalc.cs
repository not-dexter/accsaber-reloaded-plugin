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

        public APCalc()
        {
            Task.Run(Load);
        }
        private async Task Load()
        {
            AccSaberCurve? curve = await APIHandler.CallAPI_Json<AccSaberCurve>(HelpfulPaths.APAPI_CURVE_AP, AccsaberAPI.Throttler);

            if (curve is null)
            {
                Plugin.Log.Error("There was an issue parsing the curve!");
                return;
            }

            PointList = curve.Points;
            scale = curve.Scale;
            shift = curve.Shift;

            if (PointList[0].x < PointList[1].x)
                PointList.Reverse();
        }
        public float GetAp(float acc, float complexity) => GetCurve(acc) * (complexity - shift) * scale;
        public float GetAccDeflated(float deflatedPp, float complexity, int precision = -1)
        {
            if (deflatedPp > GetAp(1.0f, complexity)) return precision < 0 ? 1.0f : 100.0f;
            float outp = InvertCurve(deflatedPp / (complexity - shift) * scale);
            return precision < 0 ? outp : (float)Math.Round(outp * 100.0f, precision);
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
