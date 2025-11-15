using System.Collections.ObjectModel;
using System.Linq.Expressions;
using FastExpressionCompiler;

namespace Collector.Detection.Rules.Expressions.Predicates;

public sealed class ExpressionStarter<T>
  {
    private Expression<Func<T, bool>>? _predicate;

    internal ExpressionStarter()
      : this(false)
    {
    }

    internal ExpressionStarter(bool defaultExpression)
    {
      if (defaultExpression)
        DefaultExpression = (Expression<Func<T, bool>>) (f => true);
      else
        DefaultExpression = (Expression<Func<T, bool>>) (f => false);
    }

    internal ExpressionStarter(Expression<Func<T, bool>> exp)
      : this(false)
    {
      _predicate = exp;
    }

    private Expression<Func<T, bool>> Predicate => !IsStarted && UseDefaultExpression ? DefaultExpression! : _predicate!;

    public bool IsStarted => _predicate != null;

    public bool UseDefaultExpression => DefaultExpression != null;

    public Expression<Func<T, bool>>? DefaultExpression { get; }
    
    public Expression<Func<T, bool>> Start(Expression<Func<T, bool>> exp)
    {
      if (IsStarted)
        throw new Exception("Predicate cannot be started again.");
      return _predicate = exp;
    }

    public Expression<Func<T, bool>> Or(Expression<Func<T, bool>> expr2)
    {
      return !IsStarted ? Start(expr2) : _predicate = Predicate.Or(expr2);
    }

    public Expression<Func<T, bool>> And(Expression<Func<T, bool>> expr2)
    {
      return !IsStarted ? Start(expr2) : _predicate = Predicate.And(expr2);
    }

    public Expression<Func<T, bool>>? Not()
    {
      if (IsStarted)
        _predicate = Predicate.Not();
      else
        Start(x => false);
      return _predicate;
    }

    public override string ToString()
    {
      return Predicate.ToString();
    }
    
    public static implicit operator Expression<Func<T, bool>>(ExpressionStarter<T> right)
    {
      return right.Predicate;
    }
    
    public static implicit operator Func<T, bool>(ExpressionStarter<T> right)
    {
      return right.Predicate.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);
    }
    
    public static implicit operator ExpressionStarter<T>(Expression<Func<T, bool>> right)
    {
      return new ExpressionStarter<T>(right);
    }

    public Func<T, bool> Compile() => Predicate.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);

    public Expression Body => Predicate.Body;

    public ExpressionType NodeType => Predicate.NodeType;

    public ReadOnlyCollection<ParameterExpression> Parameters => Predicate.Parameters;

    public Type Type => Predicate.Type;

    public string? Name => Predicate.Name;

    public Type ReturnType => Predicate.ReturnType;

    public bool TailCall => Predicate.TailCall;

    public bool CanReduce => Predicate.CanReduce;
  }