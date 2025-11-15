using System.Linq.Expressions;

namespace Collector.Detection.Rules.Expressions.Predicates;

public static class PredicateBuilder
{
    public static ExpressionStarter<T> New<T>() => new();

    public static ExpressionStarter<T> New<T>(Expression<Func<T, bool>> expression)
    {
        return new ExpressionStarter<T>(expression);
    }

    public static ExpressionStarter<T> New<T>(bool defaultExpression)
    {
        return new ExpressionStarter<T>(defaultExpression);
    }

    public static ExpressionStarter<T> New<T>(IEnumerable<T> enumerable)
    {
        return New<T>();
    }

    public static ExpressionStarter<T> New<T>(
        IEnumerable<T> enumerable,
        Expression<Func<T, bool>> expression)
    {
        return New(expression);
    }

    public static ExpressionStarter<T> New<T>(IEnumerable<T> enumerable, bool state)
    {
        return New<T>(state);
    }

    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var right = new RebindParameterVisitor(expr2.Parameters[0], expr1.Parameters[0]).Visit(expr2.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(expr1.Body, right), expr1.Parameters);
    }

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expr)
    {
        return Expression.Lambda<Func<T, bool>>(Expression.Not(expr.Body), expr.Parameters);
    }

    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        Expression right = new RebindParameterVisitor(expr2.Parameters[0], expr1.Parameters[0]).Visit(expr2.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(expr1.Body, right), expr1.Parameters);
    }
}