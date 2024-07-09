using System.ComponentModel;
using System.Linq.Expressions;

namespace Aimmy2.Extensions;

internal static class ExpressionExtensions
{
    internal static T GetOwnerAs<T>(this MemberExpression memberExpression) where T : class
    {
        if (memberExpression.Expression is ConstantExpression constant)
        {
            return constant.Value as T;
        }

        if (memberExpression.Expression is MemberExpression innerMember)
        {
            var ownerObject = System.Linq.Expressions.Expression.Lambda(innerMember).Compile().DynamicInvoke();
            return ownerObject as T;
        }

        throw new ArgumentException("Invalid expression");
    }

    internal static MemberExpression GetMemberExpression<T>(this Expression<Func<T>> expression)
    {
        if (expression.Body is MemberExpression member)
        {
            return member;
        }

        if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression operand)
        {
            return operand;
        }

        throw new ArgumentException("Invalid expression");
    }
}