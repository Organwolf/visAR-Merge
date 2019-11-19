using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Math.Optimization.Losses;
using Accord.Statistics.Kernels;
using ARLocation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GPStoWorldTransformation
{
    public class SvmTransformation : GpsToWorldTransformation
    {

        // Create the sequential minimal optimization teacher
        SequentialMinimalOptimizationRegression<Polynomial> learn = new SequentialMinimalOptimizationRegression<Polynomial>()
        {
            Kernel = new Polynomial(2), // Polynomial Kernel of 2nd degree
            Complexity = 100
        };

        // Run the learning algorithm
        SupportVectorMachine<Polynomial> svm_x = null;
        SupportVectorMachine<Polynomial> svm_y = null;

        //MultivariateLinearRegression regression = null;

        //OrdinaryLeastSquares ols = new OrdinaryLeastSquares()
        //{
        //    UseIntercept = true
        //};

        public override bool TransformationAvailable => NumberOfRecords > minNumberOfPointToSolve && svm_x != null && svm_y != null;

        public override void RestartModel()
        {
            svm_x = null;
            svm_y = null;
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
                    double[] out_x = new double[records.Count];
                    double[] out_y = new double[records.Count];
                    double[][] in_XY = new double[records.Count][];
                    int idx = 0;
                    foreach (TransformationRecord r in records)
                    {
                        //out_x[idx] = r.ArPosition.x;
                        //out_y[idx] = r.ArPosition.y;
                        out_x[idx] = r.ArPosition.x;
                        out_y[idx] = r.ArPosition.y;
                        in_XY[idx] = new double[] { r.GpsPosition.Longitude, r.GpsPosition.Latitude };
                        idx++;
                    }

                    svm_x = learn.Learn(in_XY, out_x);
                    svm_y = learn.Learn(in_XY, out_y);

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

            double[][] in_XY = new double[records.Count][];
            int idx = 0;
            foreach (Location loc in locations)
            {
                in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
                idx++;
            }
            double[] out_x = svm_x.Score(in_XY);
            double[] out_y = svm_y.Score(in_XY);
            List<Vector3> output = new List<Vector3>();
            for (int i = 0; i < locations.Count; i++)
            {
                output.Add(new Vector3((float)out_x[i], (float)out_y[i]));
            }

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
        //    double[] out_x = new double[arLocations.Count];
        //    double[] out_y = new double[arLocations.Count];

        //    int idx = 0;
        //    foreach (Location loc in gpsLocations)
        //    {
        //        in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
        //        idx++;
        //    }

        //    idx = 0;
        //    foreach (Vector3 v in arLocations)
        //    {
        //        out_x[idx] = v.x;
        //        out_y[idx] = v.y;
        //        idx++;
        //    }

        //    double[] out_x_prediction = svm_x.Score(in_XY);
        //    double[] out_y_prediction = svm_y.Score(in_XY);

        //    double error_x = new SquareLoss(out_x).Loss(out_x_prediction);
        //    double error_y = new SquareLoss(out_y).Loss(out_y_prediction);
        //    error = Math.Sqrt(error_x * error_x + error_y * error_y);

        //    //r2 = svm_x.CoefficientOfDetermination(in_XY, out_xy);

        //    List<Vector3> output = new List<Vector3>();
        //    for (int i = 0; i < arLocations.Count; i++)
        //    {
        //        output.Add(new Vector3((float)out_x_prediction[i],
        //                               (float)out_y_prediction[i]));
        //    }

        //    return output;
        //}

        
    }
}
