using Microsoft.EntityFrameworkCore.Query;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AccountUnitTests
{
    public class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
        {
            return new TestAsyncEnumerable<TResult>(expression);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(_inner.Execute<TResult>(expression));
        }

        TResult IAsyncQueryProvider.ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var resultType = typeof(TResult);

            if(resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = resultType.GetGenericArguments()[0];
                var executeMethod = typeof(TestAsyncQueryProvider<TEntity>).GetMethod(nameof(ExecuteAsyncInternal), BindingFlags.NonPublic | BindingFlags.Instance);
                var genericExecuteMethod = executeMethod.MakeGenericMethod(innerType);

                return (TResult)genericExecuteMethod.Invoke(this, new object[] { expression, cancellationToken });
            }
            throw new InvalidOperationException("Invalid result type for ExecuteAsync");
        }

        private async Task<TResult> ExecuteAsyncInternal<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var result = _inner.Execute<TResult>(expression);
            if(result is Task<TResult> taskResult)
            {
                return await taskResult.ConfigureAwait(false);
            }
            return result;
        }
    }
}
