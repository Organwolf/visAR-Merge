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
    public class MultivariateLinearRegressionOnXYZTransformation : GpsToWorldTransformation
    {

        MultivariateLinearRegression regression_x = null;
        MultivariateLinearRegression regression_z = null;
        MultivariateLinearRegression regression_y = null;

        OrdinaryLeastSquares ols_x = new OrdinaryLeastSquares()
        {
            UseIntercept = true
        };
        OrdinaryLeastSquares ols_z = new OrdinaryLeastSquares()
        {
            UseIntercept = true
        };
        OrdinaryLeastSquares ols_y = new OrdinaryLeastSquares()
        {
            UseIntercept = true
        };

        public override bool TransformationAvailable => NumberOfRecords > minNumberOfPointToSolve && regression_x != null && regression_z != null && regression_y != null;

        public override void RestartModel()
        {
            regression_x = null;
            regression_z = null;
            regression_y = null;
        }

        public override void SolveTransforamtion()
        {
            //https://numerics.mathdotnet.com/Regression.html#Regularization

            if (records.Count > minNumberOfPointToSolve)
                if (records.Count % caclulationSteps == 0 || records.Count == minNumberOfPointToSolve)
                {
                    double[][] out_x = new double[records.Count][];
                    double[][] out_z = new double[records.Count][];
                    double[][] out_y = new double[records.Count][];
                    double[][] in_XY = new double[records.Count][];
                    double[][] in_XYZ = new double[records.Count][];
                    int idx = 0;
                    foreach (TransformationRecord r in records)
                    {
                        out_x[idx] = new double[] { r.ArPosition.x };
                        out_z[idx] = new double[] { r.ArPosition.z };
                        out_y[idx] = new double[] { r.ArPosition.y };

                        in_XY[idx] = new double[] { r.GpsPosition.Longitude, r.GpsPosition.Latitude };
                        in_XYZ[idx] = new double[] { r.GpsPosition.Longitude, r.GpsPosition.Latitude, r.GpsPosition.Altitude };
                        idx++;
                    }
                    regression_x = ols_x.Learn(in_XY, out_x);
                    regression_z = ols_z.Learn(in_XY, out_z);
                    regression_y = ols_y.Learn(in_XYZ, out_y);
                    if (CalcualteError && testRecords.Count > 0)
                    {
                        List<Vector3> testArLocationsPredicted = TransformGpsToWorld(testRecords.GetGPSLocations());
                        Error_Horizontal = CalculateRMSE(testArLocationsPredicted, testRecords.GetARPositions());
                    }                    
                }
        }

        

        public override List<Vector3> TransformGpsToWorld(List<Location> locations)
        {
            if (!TransformationAvailable)
                throw new Exception("Transformation is not available yet.");

            if (locations.Count <= 0)
                throw new ArgumentException("The input array of locations is empty");

            double[][] in_XY = new double[locations.Count][];
            double[][] in_XYZ = new double[locations.Count][];
            int idx = 0;
            foreach (Location loc in locations)
            {
                in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
                in_XYZ[idx] = new double[] { loc.Longitude, loc.Latitude, loc.Altitude };
                idx++;
            }
            double[][] out_x = regression_x.Transform(in_XY);
            double[][] out_z = regression_z.Transform(in_XY);
            double[][] out_y = regression_y.Transform(in_XYZ);
            List<Vector3> output = new List<Vector3>();

            for (int i = 0; i < locations.Count; i++)
            {
                output.Add(new Vector3((float)out_x[i][0], (float)out_y[i][0], (float)out_z[i][0]));
            }

            return output;
        }       

        //public override Vector3 TransformGpsToWorld(Location location)
        //{
        //    if (!TransformationAvailable)
        //        throw new Exception("Transformation is not available yet.");

        //    double[][] in_XY = new double[][] { new double[] { location.Longitude, location.Latitude, location.Altitude } };
        //    double[][] in_XYZ = new double[][] { new double[] { location.Longitude, location.Latitude, location.Altitude } };
        //    double[][] out_x = regression_x.Transform(in_XY);
        //    double[][] out_z = regression_z.Transform(in_XY);
        //    double[][] out_y = regression_y.Transform(in_XYZ);
        //    return new Vector3((float)out_x[0][0], (float)out_y[0][0], (float)out_z[0][0]);
        //}
    }
}
