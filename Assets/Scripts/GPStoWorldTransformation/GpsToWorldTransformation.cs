using Accord.Math.Optimization.Losses;
using ARLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.GPStoWorldTransformation
{
    public abstract class GpsToWorldTransformation
    {
        protected TransformationRecords records = new TransformationRecords();
        protected TransformationRecords testRecords = new TransformationRecords();
        protected double minDistDataCollectionThreshold = 0.02;
        //protected int minNumberOfPointToSolve = 20;
        protected int minNumberOfPointToSolve = 2;
        protected int caclulationSteps = 10;
        protected int maxRecordSize = 200;
        //protected double minAccuracy = 5;
        protected double minAccuracy = 10;
        protected bool gatherTestDataEnabled = true;
        protected int testRecordSelectionStep = 5;
        protected int maxTestRecordSize = 50;
        public bool CalcualteError = true;

        public double Error_Horizontal { get; protected set; }

        public int NumberOfRecords => records.Count;
        public int NumberOfTestRecords => testRecords.Count;
        public TransformationRecords Records => records;

        public int Percentage => (int)(NumberOfRecords / (float)minNumberOfPointToSolve * 100f);

        public virtual void Restart()
        {
            records = new TransformationRecords();
            testRecords = new TransformationRecords();
            Error_Horizontal = 0;
            RestartModel();
        }

        public abstract void RestartModel();

        private void addRecord(TransformationRecord record, ref TransformationRecords records, int maxRecordSize)
        {
            if (records.Count <= 0)
                records.Add(record);
            else
            {
                double distToPrevPoint = DVector2.EuclDistance(records[records.Count - 1].ArPositionAsDVector2, record.ArPositionAsDVector2);
                if (!records[records.Count - 1].ArPositionAsDVector2.Equals(record.ArPositionAsDVector2) && distToPrevPoint > minDistDataCollectionThreshold)
                    records.Add(record);
            }
            if (records.Count > maxRecordSize)
                records.RemoveAt(0);
        }
        public void AddRecord(TransformationRecord record)
        {
            if (record.GpsPosition.Accuracy > minAccuracy)
                return;

            if ((NumberOfRecords + NumberOfTestRecords) % testRecordSelectionStep == 0 && this.records.Count > 0)
                addRecord(record, ref this.testRecords, maxTestRecordSize);
            else
                addRecord(record, ref this.records, maxRecordSize);
            //{
            //    if (testRecords.Count <= 0)
            //        testRecords.Add(record);
            //    else
            //    {
            //        double distToPrevPoint = DVector2.EuclDistance(testRecords[testRecords.Count - 1].ArPositionAsDVector2, record.ArPositionAsDVector2);
            //        if (!testRecords[testRecords.Count - 1].ArPositionAsDVector2.Equals(record.ArPositionAsDVector2) && distToPrevPoint > minDistDataCollectionThreshold)
            //            testRecords.Add(record);
            //    }
            //    if (testRecords.Count > maxTestRecordSize)
            //        testRecords.RemoveAt(0);
            //}
            //else
            //{
            //    if (records.Count <= 0)
            //        records.Add(record);
            //    else
            //    {
            //        double distToPrevPoint = DVector2.EuclDistance(records[records.Count - 1].ArPositionAsDVector2, record.ArPositionAsDVector2);
            //        if (!records[records.Count - 1].ArPositionAsDVector2.Equals(record.ArPositionAsDVector2) && distToPrevPoint > minDistDataCollectionThreshold)
            //            records.Add(record);
            //    }
            //    if (records.Count > maxRecordSize)
            //        records.RemoveAt(0);
            //}

        }

        protected double CalculateRMSE(List<Vector3> arLocationsPredicted, List<Vector3> arLocationsExpected)
        {
            if (arLocationsPredicted.Count != arLocationsExpected.Count)
                throw new ArgumentException("The number of predicted and expected arrays do not match.");
            if (arLocationsPredicted.Count <= 0)
                return 0;

            double[][] out_x_expected = new double[arLocationsExpected.Count][];
            double[][] out_z_expected = new double[arLocationsExpected.Count][];
            double[][] out_y_expected = new double[arLocationsExpected.Count][];

            double[][] out_x_predicted = new double[arLocationsPredicted.Count][];
            double[][] out_z_predicted = new double[arLocationsPredicted.Count][];
            double[][] out_y_predicted = new double[arLocationsPredicted.Count][];


            //for (int i = 0; i < arLocationsExpected.Count; i++)
            //{
            //    out_x_expected[i] = new double[] { arLocationsExpected[i].x };
            //    out_z_expected[i] = new double[] { arLocationsExpected[i].z };
            //    out_y_expected[i] = new double[] { arLocationsExpected[i].y };
            //    out_x_predicted[i] = new double[] { arLocationsPredicted[i].x };
            //    out_z_predicted[i] = new double[] { arLocationsPredicted[i].z };
            //    out_y_predicted[i] = new double[] { arLocationsPredicted[i].y };
            //}
            //double error_x = new SquareLoss(out_x_expected).Loss(out_x_predicted);
            //double error_z = new SquareLoss(out_z_expected).Loss(out_x_predicted);
            //double error_y = new SquareLoss(out_y_expected).Loss(out_x_predicted);
            double error_x = 0;
            double error_z = 0;
            for (int i = 0; i < arLocationsExpected.Count; i++)
            {
                error_x += Math.Pow(arLocationsExpected[i].x - arLocationsPredicted[i].x, 2);
                error_z += Math.Pow(arLocationsExpected[i].z - arLocationsPredicted[i].z, 2);
            }
            error_x = error_x / arLocationsExpected.Count;
            error_z = error_z / arLocationsExpected.Count;

            return Math.Sqrt(error_x + error_z) / 2;
        }

        public List<Vector3> TransformGpsToWorld(List<Location> gpsLocations, List<Vector3> arLocations, out double error)
        {
            error = 0;
            if (gpsLocations.Count != arLocations.Count)
                throw new ArgumentException("The number of GPS Locations and AR World Locations are not equal.");

            List<Vector3> arLocationsPredicted = TransformGpsToWorld(gpsLocations);
            error = CalculateRMSE(arLocationsPredicted, arLocations);

            return arLocationsPredicted;


            //error = 0;
            //if (!TransformationAvailable)
            //    throw new Exception("Transformation is not available yet.");

            //if (gpsLocations.Count <= 0 || arLocations.Count <= 0)
            //    throw new ArgumentException("The input arrays of locations are empty");

            //if (gpsLocations.Count != arLocations.Count)
            //    throw new ArgumentException("The number of GPS Locations and AR World Locations are not equal.");

            //double[][] in_XY = new double[gpsLocations.Count][];
            //double[][] in_XYZ = new double[gpsLocations.Count][];
            //double[][] out_x = new double[arLocations.Count][];
            //double[][] out_z = new double[arLocations.Count][];
            //double[][] out_y = new double[arLocations.Count][];

            //int idx = 0;
            //foreach (Location loc in gpsLocations)
            //{
            //    in_XY[idx] = new double[] { loc.Longitude, loc.Latitude };
            //    in_XYZ[idx] = new double[] { loc.Longitude, loc.Latitude, loc.Altitude };
            //    out_x[idx] = new double[] { arLocations[idx].x };
            //    out_z[idx] = new double[] { arLocations[idx].z };
            //    out_y[idx] = new double[] { arLocations[idx].y };
            //    idx++;
            //}

            //double[][] out_x_prediction = regression_x.Transform(in_XY);
            //double[][] out_z_prediction = regression_z.Transform(in_XY);
            //double[][] out_y_prediction = regression_y.Transform(in_XYZ);

            //double error_x = new SquareLoss(out_x).Loss(out_x_prediction);
            //double error_z = new SquareLoss(out_z).Loss(out_x_prediction);
            //double error_y = new SquareLoss(out_y).Loss(out_x_prediction);

            //double[] r2_x = regression_x.CoefficientOfDetermination(in_XY, out_x);
            //double[] r2_z = regression_z.CoefficientOfDetermination(in_XY, out_z);
            //double[] r2_y = regression_y.CoefficientOfDetermination(in_XYZ, out_y);

            //List<Vector3> output = new List<Vector3>();
            //for (int i = 0; i < arLocations.Count; i++)
            //{
            //    output.Add(new Vector3((float)out_x_prediction[i][0], (float)out_y_prediction[i][0], (float)out_z_prediction[i][0]));
            //}

            //return output;
        }

        public Vector3 TransformGpsToWorld(Location location)
        {
            //if (!TransformationAvailable)
            //    throw new Exception("Transformation is not available yet.");

            //double[][] in_XY = new double[][] { new double[] { location.Longitude, location.Latitude } };
            //double[] out_x = svm_x.Score(in_XY);
            //double[] out_y = svm_y.Score(in_XY);
            //return new Vector3((float)out_x[0], (float)out_y[0]);
            List<Location> lstLocations = new List<Location>();
            lstLocations.Add(location);
            List<Vector3> result = TransformGpsToWorld(lstLocations);
            return result[0];
        }

        public List<Location> GetLastGPSPositions(int count)
        {
            List<Location> locations = new List<Location>();
            if (records.Count > count)
            {
                List<TransformationRecord> recs = records.GetRange(records.Count - count, count);
                recs.ForEach(r => locations.Add(r.GpsPosition));
            }
            return locations;
        }

        public List<Vector3> GetLastWorldPositions(int count)
        {
            List<Vector3> locations = new List<Vector3>();
            if (records.Count > count)
            {
                List<TransformationRecord> recs = records.GetRange(records.Count - count, count);
                recs.ForEach(r => locations.Add(r.ArPosition));
            }
            return locations;
        }

        public abstract bool TransformationAvailable { get; }

        public abstract void SolveTransforamtion();

        public abstract List<Vector3> TransformGpsToWorld(List<ARLocation.Location> gpsLocations);
    }
}
