using ARLocation;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CSV_extended
{
    public static List<Location> ParseCsvFileUsingResources(string pathToFile)
    {
        var parsedData = new List<Location>();

        TextAsset file = Resources.Load(pathToFile) as TextAsset;
        string[] lines = file.text.Split('\n');

        Debug.Log("Rows in csv file: " + lines.Length);

        foreach(string line in lines)
        {
            string[] split = line.Split(',');

            double longitude = double.Parse(split[0]);
            double latitude = double.Parse(split[1]);
            bool building = (split[2] == "1");
            double height = double.Parse(split[3]);
            double waterHeight = double.Parse(split[4]) / 100;           // converting from cm to m
            double nearestNeighborHeight = double.Parse(split[5]);
            double nearestNeighborWater = double.Parse(split[6]) / 100f; // converting from cm to m

            var data = new Location
            {
                Longitude = longitude,
                Latitude = latitude,
                Building = building,
                Height = height,
                WaterHeight = waterHeight,
                NearestNeighborHeight = nearestNeighborHeight,
                NearestNeighborWater = nearestNeighborWater,
            };

            parsedData.Add(data);
        }

        return parsedData;
    }

    public static Location ClosestPointGPS(List<Location> locations, Location deviceLocation)
    {
        Location closestLocation = new Location();
        double minDistance = Double.MaxValue;
        double distanceToClosestPoint = -1;

        foreach (Location location in locations)
        {
            var distance = Location.HaversineDistance(location, deviceLocation);

            if (distance <= minDistance)
            {
                minDistance = distance;
                distanceToClosestPoint = distance;
                closestLocation = location;
            }
        }
        Debug.Log("Distance to closest point: " + distanceToClosestPoint);
        return closestLocation;
    }

    public static List<Location> PointsWithinRadius(List<Location> locations, double radius, Location deviceLocation)
    {
        List<Location> locationsWithinRadius = new List<Location>();

        foreach (Location location in locations)
        {
            var distance = Location.HaversineDistance(location, deviceLocation);

            if (distance <= radius)
            {
                locationsWithinRadius.Add(location);
            }
        }
        return locationsWithinRadius;
    }
}
