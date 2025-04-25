using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace PhotonBypass.Tools;

public static class EntityExtensions
{
    public static string GetColumnName<T>(Expression<Func<T, object?>> expression)
    {
        MemberExpression? member = expression.Body as MemberExpression;

        if (member == null)
        {
            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression innerMember)
            {
                member = innerMember;
            }
        }

        if (member == null) return string.Empty;

        var propInfo = member.Member as PropertyInfo;
        if (propInfo == null) return string.Empty;

        var attr = propInfo.GetCustomAttribute<ColumnAttribute>();
        return attr?.Name ?? propInfo.Name ?? string.Empty;
    }

    public static string GetTablename<T>()
    {
        var type = typeof(T);

        var attr = type.GetCustomAttribute<TableAttribute>();
        return attr?.Name ?? type.Name ?? string.Empty;
    }
}
