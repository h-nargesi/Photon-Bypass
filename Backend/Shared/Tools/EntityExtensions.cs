using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace PhotonBypass.Tools;

public static class EntityExtensions
{
    public static string GetColumnName<T>(Expression<Func<T, object?>> expression)
    {
        var member = expression.Body as MemberExpression;

        if (member == null)
        {
            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression inner_member)
            {
                member = inner_member;
            }
        }

        if (member == null) return string.Empty;

        var prop_info = member.Member as PropertyInfo;
        if (prop_info == null) return string.Empty;

        var attr = prop_info.GetCustomAttribute<ColumnAttribute>();
        return attr?.Name ?? prop_info.Name;
    }

    public static string GetTableName<T>()
    {
        var type = typeof(T);

        var attr = type.GetCustomAttribute<TableAttribute>();
        return attr?.Name ?? type.Name;
    }
}
