using System.Collections.Concurrent;
using System.Linq.Expressions;
using Shared;

namespace Collector.Detection.Rules.Detections;

internal record DetectionExpressions(Expression<Func<WinEvent, bool>> ReducedExpression, ConcurrentDictionary<string, List<Expression<Func<WinEvent, bool>>>> ExpressionsByDetectionName);
