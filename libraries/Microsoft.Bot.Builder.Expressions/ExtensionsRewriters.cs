﻿// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Bot.Builder.Expressions
{
    public static partial class Extensions
    {
        /// <summary>
        /// Rewrite the expression by pushing not down to the leaves.
        /// </summary>
        /// <remarks>
        /// Push down not to the leaves if possible.  For and/or/not this uses DeMorgan's law and rewrites comparisons.  
        /// You can define your own behavior by setting <see cref="ExpressionEvaluator.Negation"/> to the negated evaluator.
        /// </remarks>
        /// <param name="expression">Expression to rewrite.</param>
        /// <returns>Rewritten expression.</returns>
        public static Expression PushDownNot(this Expression expression) => PushDownNot(expression, false);

        /// <summary>
        /// Rewrite expression into disjunctive normal form.
        /// </summary>
        /// <remarks>
        /// Rewrites to either a simple expression or a disjunction of conjunctions or simple expressions with not pushed down
        /// to leaves.
        /// </remarks>
        /// <param name="expression">Expression to rewrite.</param>
        /// <returns>Normalized expression.</returns>
        public static Expression DisjunctiveNormalForm(this Expression expression)
        {
            Expression result;
            var clauses = Conjunctions(expression.PushDownNot());
            var children = new List<Expression>();
            foreach (var clause in clauses)
            {
                if (clause.Type == ExpressionType.And && clause.Children.Length == 1)
                {
                    children.Add(clause.Children[0]);
                }
                else
                {
                    children.Add(clause);
                }
            }
            if (children.Count == 0)
            {
                result = Expression.ConstantExpression(false);
            }
            else if (children.Count == 1)
            {
                result = children[0];
            }
            else
            {
                result = Expression.MakeExpression(ExpressionType.Or, children.ToArray());
            }
            return result;
        }

        private static IEnumerable<Expression> Conjunctions(Expression expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.And:
                    {
                        // Each element of SoFar is a conjunction
                        // Need to combine every combination of clauses
                        var soFar = new List<Expression>();
                        var first = true;
                        foreach (var child in expression.Children)
                        {
                            var clauses = Conjunctions(child).ToList();
                            if (clauses.Count() == 0)
                            {
                                // Encountered false
                                soFar.Clear();
                                break;
                            }
                            if (first)
                            {
                                foreach (var clause in clauses)
                                {
                                    soFar.Add(clause);
                                }
                                first = false;
                            }
                            else
                            {
                                var newClauses = new List<Expression>();
                                foreach (var old in soFar)
                                {
                                    foreach (var clause in clauses)
                                    {
                                        if (clause.Type == ExpressionType.And)
                                        {
                                            var children = new List<Expression>();
                                            children.AddRange(old.Children);
                                            children.AddRange(clause.Children);
                                            newClauses.Add(Expression.MakeExpression(ExpressionType.And, children.ToArray()));
                                        }
                                        else
                                        {
                                            newClauses.Add(old);
                                        }
                                    }
                                }
                                soFar = newClauses;
                            }
                        }
                        foreach (var clause in soFar)
                        {
                            yield return clause;
                        }
                    }
                    break;
                case ExpressionType.Or:
                    {
                        foreach (var child in expression.Children)
                        {
                            foreach (var clause in Conjunctions(child))
                            {
                                yield return clause;
                            }
                        }
                    }
                    break;
                default:
                    // True becomes empty expression and false drops clause
                    if (expression is Constant cnst && cnst.Value is bool val)
                    {
                        if (val == true)
                        {
                            yield return expression;
                        }
                    }
                    else
                    {
                        yield return Expression.MakeExpression(ExpressionType.And, expression);
                    }
                    break;
            }
        }

        private static Expression PushDownNot(Expression expression, bool inNot)
        {
            var newExpr = expression;
            var negation = expression.Evaluator.Negation;
            switch (expression.Type)
            {
                case ExpressionType.And:
                case ExpressionType.Or:
                    newExpr = Expression.MakeExpression(inNot ? ExpressionType.And : ExpressionType.Or, (from child in expression.Children select PushDownNot(child, inNot)).ToArray());
                        var children = (from child in expression.Children select PushDownNot(child, passThrough, inNot)).ToArray();
                        if (children.Length == 1)
                            newExpr = children[0];
                            newExpr = Expression.MakeExpression(expression.Type == ExpressionType.And
                                ? (inNot ? ExpressionType.Or : ExpressionType.And)
                                : (inNot ? ExpressionType.And : ExpressionType.Or),
                                children);
                    break;
               case ExpressionType.Not:
                    newExpr = PushDownNot(expression.Children[0], !inNot);
                    break;
                default:
                    if (inNot)
                    {
                        if (negation != null)
                        {
                            if (expression.Type == negation.Type)
                            {
                                // Pass through like optional/ignore
                                newExpr = Expression.MakeExpression(negation, (from child in expression.Children select PushDownNot(child, true)).ToArray());
                            }
                            else
                            {
                                // Replace with negation and stop
                                newExpr = Expression.MakeExpression(negation, expression.Children);
                            }
                        }
                        else
                        {
                            // Keep not
                            newExpr = Expression.MakeExpression(ExpressionType.Not, expression);
                        }
                    }
                    break;
            }
            return newExpr;
        }
    }
}
