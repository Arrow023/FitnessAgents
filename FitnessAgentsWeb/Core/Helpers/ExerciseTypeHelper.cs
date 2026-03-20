using System.Collections.Generic;

namespace FitnessAgentsWeb.Core.Helpers
{
    /// <summary>
    /// Maps Health Connect ExerciseSessionRecord numeric type constants to display names and icons.
    /// Reference: https://developer.android.com/reference/kotlin/androidx/health/connect/client/records/ExerciseSessionRecord
    /// </summary>
    public static class ExerciseTypeHelper
    {
        private static readonly Dictionary<string, (string Name, string Icon)> ExerciseTypes = new()
        {
            ["0"]  = ("Other Workout",       "fa-solid fa-dumbbell"),
            ["2"]  = ("Badminton",            "fa-solid fa-shuttlecock"),
            ["4"]  = ("Baseball",             "fa-solid fa-baseball"),
            ["5"]  = ("Basketball",           "fa-solid fa-basketball"),
            ["8"]  = ("Biking",               "fa-solid fa-bicycle"),
            ["9"]  = ("Stationary Bike",      "fa-solid fa-bicycle"),
            ["10"] = ("Boot Camp",            "fa-solid fa-campground"),
            ["11"] = ("Boxing",               "fa-solid fa-hand-fist"),
            ["13"] = ("Calisthenics",         "fa-solid fa-person"),
            ["14"] = ("Cricket",              "fa-solid fa-cricket-bat-ball"),
            ["16"] = ("Dancing",              "fa-solid fa-music"),
            ["25"] = ("Elliptical",           "fa-solid fa-person-walking"),
            ["26"] = ("Exercise Class",       "fa-solid fa-users"),
            ["27"] = ("Fencing",              "fa-solid fa-person"),
            ["28"] = ("American Football",    "fa-solid fa-football"),
            ["29"] = ("Australian Football",  "fa-solid fa-football"),
            ["31"] = ("Frisbee",              "fa-solid fa-compact-disc"),
            ["32"] = ("Golf",                 "fa-solid fa-golf-ball-tee"),
            ["33"] = ("Guided Breathing",     "fa-solid fa-wind"),
            ["34"] = ("Gymnastics",           "fa-solid fa-person"),
            ["35"] = ("Handball",             "fa-solid fa-hand-back-fist"),
            ["36"] = ("HIIT",                 "fa-solid fa-bolt"),
            ["37"] = ("Hiking",               "fa-solid fa-mountain"),
            ["38"] = ("Ice Hockey",           "fa-solid fa-hockey-puck"),
            ["39"] = ("Ice Skating",          "fa-solid fa-person-skating"),
            ["44"] = ("Martial Arts",         "fa-solid fa-hand-fist"),
            ["46"] = ("Paddling",             "fa-solid fa-water"),
            ["47"] = ("Paragliding",          "fa-solid fa-parachute-box"),
            ["48"] = ("Pilates",              "fa-solid fa-person"),
            ["50"] = ("Racquetball",          "fa-solid fa-table-tennis-paddle-ball"),
            ["51"] = ("Rock Climbing",        "fa-solid fa-mountain"),
            ["52"] = ("Roller Hockey",        "fa-solid fa-hockey-puck"),
            ["53"] = ("Rowing",               "fa-solid fa-water"),
            ["54"] = ("Rowing Machine",       "fa-solid fa-water"),
            ["55"] = ("Rugby",                "fa-solid fa-football"),
            ["56"] = ("Running",              "fa-solid fa-person-running"),
            ["57"] = ("Treadmill",            "fa-solid fa-person-running"),
            ["58"] = ("Sailing",              "fa-solid fa-sailboat"),
            ["59"] = ("Scuba Diving",         "fa-solid fa-water"),
            ["60"] = ("Skating",              "fa-solid fa-person-skating"),
            ["61"] = ("Skiing",               "fa-solid fa-person-skiing"),
            ["62"] = ("Snowboarding",         "fa-solid fa-person-snowboarding"),
            ["63"] = ("Snowshoeing",          "fa-solid fa-person-hiking"),
            ["64"] = ("Soccer",               "fa-solid fa-futbol"),
            ["65"] = ("Softball",             "fa-solid fa-baseball"),
            ["66"] = ("Squash",               "fa-solid fa-table-tennis-paddle-ball"),
            ["68"] = ("Stair Climbing",       "fa-solid fa-stairs"),
            ["69"] = ("Stair Machine",        "fa-solid fa-stairs"),
            ["70"] = ("Strength Training",    "fa-solid fa-dumbbell"),
            ["71"] = ("Stretching",           "fa-solid fa-person"),
            ["72"] = ("Surfing",              "fa-solid fa-water"),
            ["73"] = ("Open Water Swimming",  "fa-solid fa-person-swimming"),
            ["74"] = ("Pool Swimming",        "fa-solid fa-person-swimming"),
            ["75"] = ("Table Tennis",         "fa-solid fa-table-tennis-paddle-ball"),
            ["76"] = ("Tennis",               "fa-solid fa-table-tennis-paddle-ball"),
            ["78"] = ("Volleyball",           "fa-solid fa-volleyball"),
            ["79"] = ("Walking",              "fa-solid fa-person-walking"),
            ["80"] = ("Water Polo",           "fa-solid fa-water"),
            ["81"] = ("Weightlifting",        "fa-solid fa-weight-hanging"),
            ["82"] = ("Wheelchair",           "fa-solid fa-wheelchair"),
            ["83"] = ("Yoga",                 "fa-solid fa-spa"),
        };

        public static string GetExerciseName(string typeCode)
        {
            return ExerciseTypes.TryGetValue(typeCode, out var info) ? info.Name : $"Workout ({typeCode})";
        }

        public static string GetExerciseIcon(string typeCode)
        {
            return ExerciseTypes.TryGetValue(typeCode, out var info) ? info.Icon : "fa-solid fa-dumbbell";
        }
    }
}
