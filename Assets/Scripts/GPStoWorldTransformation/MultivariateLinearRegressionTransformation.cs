using Accord.Math.Optimization.Losses;
using Accord.Statistics.Models.Regression.Linear;
using ARLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.GPStoWorldTransformation
{
    public class MultivariateLinearRegressionTransformation : GpsToWorldTransformation
    {

        MultivariateLinearRegression regression = null;

        OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
        {
            UseIntercept = true
        };

        public override bool TransformationAvailable => NumberOfRecords > minNumberOfPointToSolve && regression != null;

        public override void RestartModel()
        {
            regression = null;
        }

        public override void SolveTransforamtion()
        {
            //https://numerics.mathdotnet.com/Regression.html#Regularization

            /// <summary>
            /// Least-Squares fitting the points (X,y) = ((x0,x1,..,xk),y) to a linear surface y : X -> p0*x0 + p1*x1 + ... + pk*xk,
            /// returning a function y' for the best fitting combination.
            /// If an intercept is added, its coefficient will be prepended to the resulting parameters.
            /// </summary>
            if (records.Count > minNumberOfPointToSolve)
                if (records.Count % caclulationSteps == 0 || records.Count == minNumberOfPointToSolve)
                {

                    //double[] out_x = new double[records.Count];
                    //double[] out_y = new double[records.Count];
                    double[][] out_xy = new double[records.Count][];
                    double[][] in_XY = new double[records.Count][];
                    int idx = 0;
                    foreach (TransformationRecord r in records)
                    {
                        out_xy[idx] = new double[] { r.ArPosition.x, r.ArPosition.z };
                        in_XY[idx] = new double[] { r.GpsPosition.Longitude, r.GpsPosition.Latitude };
                        idx++;
                    }
                    regression = ols.Learn(in_XY, out_xy);
                    //// We can obtain predictions using
                    //double[][] predictions = regression.Transform(in_XY);

                    //// The prediction error is
                    //double error = new SquareLoss(out_xy).Loss(predictions); // 0
                    //double[] r2 = regression.CoefficientOfDetermination(in_XY, out_xy);
                }
        }

        public override List<Vector3> TransformGpsToWorld(List<Location> locations)
        {
            if (!TransformationAvailable)
                throw new Exception("Transformation is not available yet.");

            if (locations.Count <= 0)
                throw new ArgumentException("The input array of locations is empty");

            double[][] in_XY = new double[locations.Count][];
            int idx = 0;
            foreach (Location loc in locations)
            {
                in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
                idx++;
            }
            double[][] out_xz = regression.Transform(in_XY);
            List<Vector3> output = new List<Vector3>();
            foreach (double[] r in out_xz)
                output.Add(new Vector3((float)r[0], 0, (float)r[1]));
            return output;
        }

        //public override List<Vector3> TransformGpsToWorld(List<Location> gpsLocations, List<Vector3> arLocations, out double error)
        //{
        //    error = 0;
        //    if (!TransformationAvailable)
        //        throw new Exception("Transformation is not available yet.");

        //    if (gpsLocations.Count <= 0 || arLocations.Count <= 0)
        //        throw new ArgumentException("The input arrays of locations are empty");

        //    if (gpsLocations.Count != arLocations.Count)
        //        throw new ArgumentException("The number of GPS Locations and AR World Locations are not equal.");

        //    double[][] in_XY = new double[gpsLocations.Count][];
        //    double[][] out_xz = new double[arLocations.Count][];

        //    int idx = 0;
        //    foreach (Location loc in gpsLocations)
        //    {
        //        in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
        //        idx++;
        //    }

        //    idx = 0;
        //    foreach (Vector3 v in arLocations)
        //    {
        //        out_xz[idx] = new double[] { v.x, v.z };
        //        idx++;
        //    }

        //    double[][] out_xy_prediction = regression.Transform(in_XY);
        //    error = new SquareLoss(out_xz).Loss(out_xy_prediction);

        //    List<Vector3> output = new List<Vector3>();
        //    foreach (double[] r in out_xy_prediction)
        //        output.Add(new Vector3((float)r[0], 0, (float)r[1]));

        //    return output;
        //}

        //public override Vector3 TransformGpsToWorld(Location location)
        //{
        //    if (!TransformationAvailable)
        //        throw new Exception("Transformation is not available yet.");
            
        //    double[][] in_XY = new double[][] { new double[] { location.Longitude, location.Latitude } };                        
        //    double[][] out_xz = regression.Transform(in_XY);
        //    return new Vector3((float)out_xz[0][0], 0, (float)out_xz[0][1]);
        //}
    }
}
