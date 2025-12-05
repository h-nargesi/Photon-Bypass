using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Application;

public static class UserManagerHelper
{
    public static string CheckRateLimit(this ITikConnection connection, int megabytes)
    {
        var limitation_name = $"limit-speed-{megabytes}M";
        var limitation = connection.LoadList<LimitationModel>(
                TikParam.Equal<LimitationModel>(nameof(LimitationModel.Name),
                    limitation_name))?
            .FirstOrDefault();

        if (limitation != null) return limitation_name;

        limitation = new LimitationModel
        {
            Name = limitation_name,
            RateLimitTx = "2M",
            RateLimitRx = "2M",
        };

        connection.Save(limitation);

        return limitation_name;
    }

    public static string CheckTrafficLimit(this ITikConnection connection, long bytes)
    {
        if (bytes % StaticValues.BytesInGigLong != 0)
        {
            throw new Exception("Traffic limit is exceeding gigabytes");
        }

        var gigabytes = bytes / StaticValues.BytesInGigLong;
        if (gigabytes % 25 != 0)
        {
            throw new Exception("Traffic limit must be a multiple of 25 gigabytes.");
        }

        var limitation_name = $"limit-traffic-{gigabytes}G";
        var limitation = connection.LoadList<LimitationModel>(
                TikParam.Equal<LimitationModel>(nameof(LimitationModel.Name),
                    limitation_name))?
            .FirstOrDefault();

        if (limitation != null) return limitation_name;

        limitation = new LimitationModel
        {
            Name = limitation_name,
            TransferLimit = bytes,
        };

        connection.Save(limitation);

        return limitation_name;
    }

    public static string CheckProfile(this ITikConnection connection, int? days, long? traffic, int? speed)
    {
        var profile_name = "profile";

        int? gigabytes = null;
        if (traffic.HasValue)
        {
            if (traffic.Value % StaticValues.BytesInGigLong != 0)
            {
                throw new Exception("Traffic limit is exceeding gigabytes");
            }

            gigabytes = (int)(traffic.Value / StaticValues.BytesInGigLong);
            if (gigabytes % 25 != 0)
            {
                throw new Exception("Traffic limit must be a multiple of 25 gigabytes.");
            }

            profile_name += $"-{gigabytes}G";
        }

        if (days.HasValue)
        {
            profile_name += $"-{days.Value}d";
        }

        if (speed.HasValue)
        {
            profile_name += $"-{speed.Value}M";
        }

        var profile = connection.LoadList<ProfileModel>(
                TikParam.Equal<ProfileModel>(nameof(ProfileModel.Name),
                    profile_name))?
            .FirstOrDefault();

        if (profile != null) return profile_name;

        profile = new ProfileModel
        {
            Name = profile_name,
            Validity = days.HasValue ? $"{days}d 00:00:00" : "unlimited",
            NameForUsers = GetNameForUser(days, gigabytes, speed),
            StartsWhen = "first-auth",
        };

        connection.Save(profile);

        return profile_name;
    }

    public static void CheckLimitationAssignment(this ITikConnection connection, string profile_name, HashSet<string> limitations)
    {
        var assignments_dictionary = connection.LoadList<ProfileLimitationModel>(
                                             TikParam.Equal<ProfileLimitationModel>(
                                                 nameof(ProfileLimitationModel.Profile), profile_name))?
                                         .ToDictionary(x => x.Limitation) ??
                                     new Dictionary<string, ProfileLimitationModel>();

        var adding_list = limitations
            .Where(name => !assignments_dictionary.ContainsKey(name))
            .Select(limitation_name => new ProfileLimitationModel
            {
                Profile = profile_name,
                Limitation = limitation_name,
            })
            .ToList();

        if (adding_list.Count > 0)
        {
            connection.Save(adding_list);
        }

        var removing_list = assignments_dictionary.Where(assignment => !limitations.Contains(assignment.Key))
            .Select(assignment => assignment.Value)
            .ToList();

        if (removing_list.Count > 0)
        {
            connection.Delete(removing_list);
        }
    }

    private static string GetNameForUser(int? days, int? traffic, int? speed)
    {
        var profile_name = string.Empty;

        if (traffic.HasValue)
        {
            if (traffic % StaticValues.BytesInGigLong != 0)
            {
                throw new Exception("Traffic limit is exceeding gigabytes");
            }

            var gigabytes = traffic / StaticValues.BytesInGigLong;
            if (gigabytes % 25 != 0)
            {
                throw new Exception("Traffic limit must be a multiple of 25 gigabytes.");
            }

            profile_name += $" {gigabytes} Gigabytes";
        }

        if (days.HasValue)
        {
            profile_name += $" {days} Days";
        }

        if (speed.HasValue)
        {
            profile_name += $" {speed} M";
        }

        return profile_name.Length == 0 ? "Unlimited" : profile_name.Remove(0, 1);
    }
}