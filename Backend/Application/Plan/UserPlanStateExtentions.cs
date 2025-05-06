using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Application.Plan;

static class UserPlanStateExtentions
{
    public static string GetRemainsTitle(this UserPlanStateEntity entity)
    {
        var result = string.Empty;
        if (entity.PlanType == PlanType.Traffic)
        {
            result = $"{entity.GigaLeft?.ToString() ?? "--"} گیگ باقی مانده";
        }
        else
        {
            if (entity.LeftDays > 0)
            {
                result = $"و {entity.LeftDays} روز";
            }

            if (entity.LeftHours > 0 || !(entity.LeftDays > 0))
            {
                result = $"و {entity.LeftHours?.ToString() ?? "--"} ساعت";
            }
        }

        return result;
    }
}
