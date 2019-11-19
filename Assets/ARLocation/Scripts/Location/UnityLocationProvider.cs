using UnityEngine;
using M = System.Math;

namespace ARLocation
{

    public class UnityLocationProvider : AbstractLocationProvider
    {
        private float androidMagneticDeclination;
        public override string Name => "UnityLocationProvider";

        public override bool IsCompassEnabled => Input.compass.enabled;

        protected override void RequestLocationAndCompassUpdates()
        {
            Debug.Log("[UnityLocationProvider]: Requesting location updates...");

            Input.compass.enabled = true;

            Input.location.Start(
                (float)Options.AccuracyRadius,
                (float)Options.MinDistanceBetweenUpdates
            );
        }

        protected override void InnerOnEnabled()
        {
            androidMagneticDeclination = AndroidMagneticDeclination.GetDeclination(CurrentLocation.ToLocation());
        }

        protected override void UpdateLocationRequestStatus()
        {
            switch (Input.location.status)
            {
                case LocationServiceStatus.Initializing:
                    Status = LocationProviderStatus.Initializing;
                    break;

                case LocationServiceStatus.Failed:
                    Status = LocationProviderStatus.Failed;
                    break;

                case LocationServiceStatus.Running:
                    Status = LocationProviderStatus.Started;
                    break;

                case LocationServiceStatus.Stopped:
                    Status = LocationProviderStatus.Idle;
                    break;
            }
        }

        protected override LocationReading? ReadLocation()
        {
            if (!HasStarted)
            {
                return null;
            }

            var data = Input.location.lastData;

            return new LocationReading()
            {
                latitude = data.latitude,
                longitude = data.longitude,
                altitude = data.altitude,
                accuracy = data.horizontalAccuracy,
                floor = -1,
                timestamp = (long)(data.timestamp * 1000)
            };
        }

        protected override HeadingReading? ReadHeading()
        {
            if (!HasStarted)
            {
                return null;
            }

            // ReSharper disable once RedundantAssignment
            var magneticHeading = Input.compass.magneticHeading;

            // ReSharper disable once RedundantAssignment
            var trueHeading = Input.compass.trueHeading;

#if PLATFORM_ANDROID
            var tiltCorrectedMagneticHeading = GetMagneticHeading();
            magneticHeading = tiltCorrectedMagneticHeading;
            trueHeading = tiltCorrectedMagneticHeading + androidMagneticDeclination;
#endif

            return new HeadingReading()
            {
                heading = trueHeading,
                magneticHeading = magneticHeading,
                accuracy = Input.compass.headingAccuracy,
                timestamp = (long)(Input.compass.timestamp * 1000),
                isMagneticHeadingAvailable = Input.compass.enabled
            };
        }

        private float GetMagneticHeading()
        {
#if PLATFORM_ANDROID
            if (!SystemInfo.supportsGyroscope || !ApplyCompassTiltCompensationOnAndroid)
            {
                return Input.compass.magneticHeading;
            }

            Input.gyro.enabled = true;

            var g = Input.gyro.gravity;
            var b = Input.compass.rawVector;

            switch (Input.deviceOrientation)
            {
                case DeviceOrientation.LandscapeLeft:
                    b = new Vector3(
                        Input.compass.rawVector.y,
                        -Input.compass.rawVector.x,
                        Input.compass.rawVector.z);
                    break;
                case DeviceOrientation.LandscapeRight:
                    b = new Vector3(
                        -Input.compass.rawVector.y,
                        Input.compass.rawVector.x,
                        Input.compass.rawVector.z
                        );
                    break;
            }

            var phi = Mathf.Atan2(g.y, g.z);
            var theta = Mathf.Atan(- g.x / (g.y * Mathf.Sin(phi) + g.z * Mathf.Cos(phi)));
            var psi = Mathf.Atan2(b.z * Mathf.Sin(phi) - b.y * Mathf.Cos(phi),
                b.x * Mathf.Cos(theta) + b.y * Mathf.Sin(theta) * Mathf.Sin(phi) +
                b.z * Mathf.Sin(theta) * Mathf.Cos(phi));

            var heading = (MathUtils.RadiansToDegrees(psi + Mathf.PI) + 90.0f) % 360.0f;

            return heading;
#else
            return Input.compass.magneticHeading;
#endif
        }
    }
}
