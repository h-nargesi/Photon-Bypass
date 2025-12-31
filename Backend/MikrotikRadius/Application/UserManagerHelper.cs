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

    public static bool GetProfile(this ITikConnection connection, int? days, long? traffic, int? rate,
        out ProfileModel profile)
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

        profile = connection.LoadList<ProfileModel>(
                TikParam.Equal<ProfileModel>(nameof(ProfileModel.Name),
                    profile_name))?
            .FirstOrDefault()!;

        if (profile != null)
        {
            return true;
        }

        profile = new ProfileModel
        {
            Name = profile_name,
            Validity = days.HasValue ? $"{days}d 00:00:00" : "unlimited",
            NameForUsers = GetNameForUser(days, gigabytes, rate),
            StartsWhen = ProfileModel.StartsWhenType.Assigned,
        };

        return false;
    }

    public static string CheckProfile(this ITikConnection connection, int? days, long? traffic, int? rate)
    {
        if (connection.GetProfile(days, traffic, rate, out var profile)) return profile.Name;

        connection.Save(profile);

        return profile.Name;
    }

    public static void GetLimitationAssignment(this ITikConnection connection, string profile_name,
        HashSet<string> limitations,
        out List<ProfileLimitationModel> adding_list, out List<ProfileLimitationModel> removing_list)
    {
        var profile_field_filter =
            TikParam.Equal<ProfileLimitationModel>(nameof(ProfileLimitationModel.Profile), profile_name);
        var assignments_dictionary = connection.LoadList<ProfileLimitationModel>(profile_field_filter)?
            .ToDictionary(x => x.Limitation) ?? [];

        adding_list = limitations
            .Where(name => !assignments_dictionary.ContainsKey(name))
            .Select(limitation_name => new ProfileLimitationModel
            {
                Profile = profile_name,
                Limitation = limitation_name,
            })
            .ToList();

        removing_list = assignments_dictionary.Where(assignment => !limitations.Contains(assignment.Key))
            .Select(assignment => assignment.Value)
            .ToList();
    }

    public static void CheckLimitationAssignment(this ITikConnection connection, string profile_name,
        HashSet<string> limitations)
    {
        connection.GetLimitationAssignment(profile_name, limitations, out var adding_list, out var removing_list);

        foreach (var add in adding_list)
        {
            connection.Save(add);
        }

        foreach (var remove in removing_list)
        {
            connection.Delete(remove);
        }
    }

    public static bool GetUser(this ITikConnection connection, AccountEntity account, int shared_user,
        out UserModel user)
    {
        var no_need_to_save = true;
        user = connection.GetUser(account.Username)
               ?? new UserModel
               {
                   Name = account.Username,
                   Password = account.VpnPassword,
               };

        if (user.Id == null)
        {
            no_need_to_save = false;
        }

        if (user.Disabled)
        {
            user.Disabled = false;
            no_need_to_save = false;
        }

        if (user.SharedUsers != shared_user)
        {
            user.SharedUsers = shared_user;
            no_need_to_save = false;
        }

        return no_need_to_save;
    }

    public static void CheckUser(this ITikConnection connection, AccountEntity account, int shared_user)
    {
        if (!connection.GetUser(account, shared_user, out var user))
        {
            connection.Save(user);
        }
    }

    public static UserModel? GetUser(this ITikConnection connection, string username)
    {
        var username_filter = TikParam.Equal<UserModel>(nameof(UserModel.Name), username);
        return connection.LoadList<UserModel>(username_filter).FirstOrDefault();
    }

    public static void AssignUserProfile(this ITikConnection connection, string username, string profile_name)
    {
        connection.Save(new UserProfileModel
        {
            Username = username,
            Profile = profile_name,
        });
    }

    public static bool GetRateLimit(this ITikConnection connection, int rate, out LimitationModel limitation)
    {
        var limitation_name = $"limit-speed-{rate}M";
        limitation = connection.LoadList<LimitationModel>(
                TikParam.Equal<LimitationModel>(nameof(LimitationModel.Name),
                    limitation_name))?
            .FirstOrDefault()!;

        if (limitation != null)
        {
            return true;
        }

        limitation = new LimitationModel
        {
            Name = limitation_name,
            RateLimitTx = "2M",
            RateLimitRx = "2M",
        };

        return false;
    }

    private static string CheckRateLimit(this ITikConnection connection, int rate)
    {
        if (connection.GetRateLimit(rate, out var limitation)) return limitation.Name;

        connection.Save(limitation);

        return limitation.Name;
    }

    public static bool GetTrafficLimit(this ITikConnection connection, long bytes, out LimitationModel limitation)
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
        limitation = connection.LoadList<LimitationModel>(
                TikParam.Equal<LimitationModel>(nameof(LimitationModel.Name),
                    limitation_name))?
            .FirstOrDefault()!;

        if (limitation != null)
        {
            return true;
        }

        limitation = new LimitationModel
        {
            Name = limitation_name,
            TransferLimit = bytes,
        };

        return false;
    }

    private static string CheckTrafficLimit(this ITikConnection connection, long bytes)
    {
        if (connection.GetTrafficLimit(bytes, out var limitation)) return limitation.Name;

        connection.Save(limitation);

        return limitation.Name;
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