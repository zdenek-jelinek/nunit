// ***********************************************************************
// Copyright (c) 2017-2018 Charlie Poole, Rob Prouse
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Threading;
#if !THREAD_ABORT
using System.Threading.Tasks;
#endif
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal.Abstractions;

namespace NUnit.Framework.Internal.Commands
{
    /// <summary>
    /// <see cref="TimeoutCommand"/> creates a timer in order to cancel
    /// a test if it exceeds a specified time and adjusts
    /// the test result if it did time out.
    /// </summary>
    public class TimeoutCommand : BeforeAndAfterTestCommand
    {
        private readonly int _timeout;
        private readonly IDebugger _debugger;
#if THREAD_ABORT
        Timer _commandTimer;
        private bool _commandTimedOut;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutCommand"/> class.
        /// </summary>
        /// <param name="innerCommand">The inner command</param>
        /// <param name="timeout">Timeout value</param>
        /// <param name="debugger">An <see cref="IDebugger"/> instance</param>
        public TimeoutCommand(TestCommand innerCommand, int timeout, IDebugger debugger) : base(innerCommand)
        {
            _timeout = timeout;
            _debugger = debugger;

            Guard.ArgumentValid(innerCommand.Test is TestMethod, "TimeoutCommand may only apply to a TestMethod", nameof(innerCommand));
            Guard.ArgumentValid(timeout > 0, "Timeout value must be greater than zero", nameof(timeout));
            Guard.ArgumentNotNull(debugger, nameof(debugger));

#if THREAD_ABORT
            BeforeTest = (context) =>
            {
                var testThread = Thread.CurrentThread;
                var nativeThreadId = ThreadUtility.GetCurrentThreadNativeId();

                // Create a timer to cancel the current thread
                _commandTimer = new Timer(
                    (o) =>
                    {
                        _commandTimedOut = true;

                        if (_debugger.IsAttached)
                        {
                            return;
                        }

                        ThreadUtility.Abort(testThread, nativeThreadId);
                        // No join here, since the thread doesn't really terminate
                    },
                    null,
                    timeout,
                    Timeout.Infinite);
            };

            AfterTest = (context) =>
            {
                _commandTimer.Dispose();

                if (ShouldReportTimeout(context.CurrentResult.ResultState))
                {
                    context.CurrentResult.SetResult(ResultState.Failure, $"Test exceeded Timeout value of {timeout}ms");
                }
            };
#else
            BeforeTest = _ => { };
            AfterTest = _ => { };
#endif
        }

#if THREAD_ABORT
        private bool ShouldReportTimeout(ResultState resultState)
        {
            if (_debugger.IsAttached)
            {
                return _commandTimedOut
                    && (resultState.Status != TestStatus.Failed || resultState.Label == ResultState.Cancelled.Label);
            }
            else
            {
                return _commandTimedOut;
            }
        }
#endif

#if !THREAD_ABORT
        /// <summary>
        /// Runs the test, saving a TestResult in the supplied TestExecutionContext.
        /// </summary>
        /// <param name="context">The context in which the test should run.</param>
        /// <returns>A TestResult</returns>
        public override TestResult Execute(TestExecutionContext context)
        {
            if (_debugger.IsAttached)
            {
                return ExecuteWithDebugger(context);
            }
            else
            {
                return ExecuteWithoutDebugger(context);
            }
        }

        private TestResult ExecuteWithoutDebugger(TestExecutionContext context)
        {
            try
            {
                var testExecution = RunTestWithTimeoutAsync(context);
                if (!testExecution.IsCompleted)
                {
                    SetResultToTimeoutFailure(context);
                }
            }
            catch (Exception exception)
            {
                context.CurrentResult.RecordException(exception, FailureSite.Test);
            }

            return context.CurrentResult;
        }

        private TestResult ExecuteWithDebugger(TestExecutionContext context)
        {
            bool completedInTime = false;
            bool started = false;
            try
            {
                var testExecution = RunTestWithTimeoutAsync(context);
                started = true;

                completedInTime = testExecution.IsCompleted;

                AwaitCompletionWithoutExceptionWrapping(testExecution);

                if (!completedInTime && !IsFailure(context.CurrentResult.ResultState))
                {
                    SetResultToTimeoutFailure(context);
                }
            }
            catch (Exception exception)
            {
                if (!started || completedInTime || IsFailure(exception))
                {
                    context.CurrentResult.RecordException(exception, FailureSite.Test);
                }
                else
                {
                    SetResultToTimeoutFailure(context);
                }
            }

            return context.CurrentResult;
        }

        private Task RunTestWithTimeoutAsync(TestExecutionContext context)
        {
            var testExecution = Task.Run(() => context.CurrentResult = innerCommand.Execute(context));

            AwaitCompletionWithoutExceptionWrapping(testExecution, _timeout);

            return testExecution;
        }

        private void AwaitCompletionWithoutExceptionWrapping(Task<TestResult> testExecution, int timeoutMilliseconds)
        {
            var timeout = Task
                .WhenAny(testExecution, Task.Delay(timeoutMilliseconds))
                .Unwrap();

            AwaitCompletionWithoutExceptionWrapping(timeout);
        }

        private static void AwaitCompletionWithoutExceptionWrapping(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        private void SetResultToTimeoutFailure(TestExecutionContext context)
        {
            context.CurrentResult.SetResult(new ResultState(
                TestStatus.Failed,
                $"Test exceeded Timeout value {_timeout}ms.",
                FailureSite.Test));
        }

        private static bool IsFailure(Exception exception)
        {
            var resultStateException = UnwrapResultStateException(exception);
            return resultStateException is null
                || IsFailure(resultStateException.ResultState);
        }

        private static bool IsFailure(ResultState resultState)
        {
            return resultState.Status == TestStatus.Failed;
        }

        private static ResultStateException UnwrapResultStateException(Exception exception)
        {
            while (true)
            {
                if (exception is ResultStateException resultStateException)
                {
                    return resultStateException;
                }

                if (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }
                else
                {
                    return null;
                }
            }
        }
#endif
    }
}
