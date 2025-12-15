using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Application;

public static class UserManagerHelper
{
    public static HashSet<string> CheckLimitations(this ITikConnection connection, RenewalEntity renewal)
    {
        var limitations = new HashSet<string>();

        // Check Speed
        if (renewal.RateLimitInMeg.HasValue)
        {
            limitations.Add(connection.CheckRateLimit(renewal.RateLimitInMeg.Value));
        }

        // Check Traffic
        if (renewal.TrafficLimit.HasValue)
        {
            limitations.Add(connection.CheckTrafficLimit(renewal.TrafficLimit.Value));
        }

        return limitations;
    }

    public static string CheckProfile(this ITikConnection connection, int? days, long? traffic, int? rate)
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

        if (rate.HasValue)
        {
            profile_name += $"-{rate.Value}M";
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
            NameForUsers = GetNameForUser(days, gigabytes, rate),
            StartsWhen = "first-auth",
        };

        connection.Save(profile);

        return profile_name;
    }

    public static void CheckLimitationAssignment(this ITikConnection connection, string profile_name,
        HashSet<string> limitations)
    {
        var profile_field_filter =
            TikParam.Equal<ProfileLimitationModel>(nameof(ProfileLimitationModel.Profile), profile_name);
        var assignments_dictionary = connection.LoadList<ProfileLimitationModel>(profile_field_filter)?
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

        foreach (var add in adding_list)
        {
            connection.Save(add);
        }

        var removing_list = assignments_dictionary.Where(assignment => !limitations.Contains(assignment.Key))
            .Select(assignment => assignment.Value)
            .ToList();

        foreach (var remove in removing_list)
        {
            connection.Delete(remove);
        }
    }

    public static void CheckUser(this ITikConnection connection, AccountEntity account, int shared_user)
    {
        var needs_save = false;
        var username_filter = TikParam.Equal<UserModel>(nameof(UserModel.Name), account.Username);
        var user = connection.LoadList<UserModel>(username_filter).FirstOrDefault();

        if (user == null)
        {
            user = new UserModel
            {
                Name = account.Username,
                Password = account.VpnPassword,
            };
            needs_save = true;
        }

        if (user.Disabled)
        {
            user.Disabled = false;
            needs_save = true;
        }

        if (user.SharedUsers != shared_user)
        {
            user.SharedUsers = shared_user;
            needs_save = true;
        }

        if (needs_save)
        {
            connection.Save(user);
        }
    }

    public static void AssignUserProfile(this ITikConnection connection, string username, string profile_name)
    {
        connection.Save(new UserProfileModel
        {
            Username = username,
            Profile = profile_name,
        });
    }

    private static string CheckRateLimit(this ITikConnection connection, int rate)
    {
        var limitation_name = $"limit-speed-{rate}M";
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

    private static string CheckTrafficLimit(this ITikConnection connection, long bytes)
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

    private static string GetNameForUser(int? days, int? gigabytes, int? speed)
    {
        var profile_name = string.Empty;

        if (gigabytes.HasValue)
        {
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