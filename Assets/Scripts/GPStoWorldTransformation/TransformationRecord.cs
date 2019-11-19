using ARLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.GPStoWorldTransformation
{
    public class TransformationRecord
    {
        public Location GpsPosition { get; private set; }
        public Vector3 ArPosition { get; private set; }
        public DateTime RegistrationTime { get; private set; }

        public TransformationRecord(Location gpsPos, Vector3 arPos, DateTime registrationTime)
        {
            ArPosition = arPos;
            GpsPosition = gpsPos;
            RegistrationTime = registrationTime;
        }

        public DVector2 ArPositionAsDVector2
        {
            get
            {
                return new DVector2(ArPosition.x, ArPosition.y);
            }
        }
    }

    public class TransformationRecords: List<TransformationRecord>
    {
        public List<Location> GetGPSLocations()
        {
            List<Location> lst = new List<Location>();
            foreach (TransformationRecord rec in this)
                lst.Add(rec.GpsPosition);
            return lst;
        }
        public List<Vector3> GetARPositions()
        {
            List<Vector3> lst = new List<Vector3>();
            foreach (TransformationRecord rec in this)
                lst.Add(rec.ArPosition);
            return lst;
        }
    }
}
