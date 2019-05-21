﻿using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
#nullable enable
namespace System
{
    public readonly ref struct BindedScope
    {
        private readonly Action _undoAction;
        private readonly CancellationTokenSource _cts;

        private BindedScope(Action undoAction)
        {
            _undoAction = undoAction;
            _cts = new CancellationTokenSource();
        }

        private static Action CreateAction<TResult>(object? obj, MemberInfo? memberInfo, ref TResult newValue)
        {
            TResult oldValue;

            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    oldValue = (TResult)propertyInfo.GetMethod.Invoke(obj, Array.Empty<object>());
                    propertyInfo.SetMethod.Invoke(obj, new object[] { newValue! });
                    return () => propertyInfo.SetMethod.Invoke(obj, new object[] { oldValue });
                case FieldInfo fieldInfo:
                    oldValue = (TResult)fieldInfo.GetValue(obj);
                    fieldInfo.SetValue(obj, newValue);
                    return () => fieldInfo.SetValue(obj, oldValue);
                default:
                    throw new InvalidOperationException("The argument memberInfo must be a Property or a Field");
            }
        }

        public static BindedScope Create<TValue>(Expression<Func<TValue>> expression, TValue newValue)
        {
            var memberInfo = (expression.Body as MemberExpression)?.Member;
            var undoFunc = CreateAction(null, memberInfo, ref newValue);
            return new BindedScope(undoFunc);
        }

        public static BindedScope Create<T, TValue>(T obj, Expression<Func<T, TValue>> expression, TValue newValue)
        {
            var memberInfo = (expression.Body as MemberExpression)?.Member;
            var undoFunc = CreateAction(obj, memberInfo, ref newValue);
            return new BindedScope(undoFunc);
        }

        public void Cancel()
            => _cts.Cancel();

        public void Dispose()
        {
            if (!_cts.IsCancellationRequested)
                _undoAction();

            _cts.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (!_cts.IsCancellationRequested)
                _undoAction();

            _cts.Dispose();
            return new ValueTask();
        }
    }
}