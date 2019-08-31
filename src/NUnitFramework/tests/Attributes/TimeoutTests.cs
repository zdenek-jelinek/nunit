// ***********************************************************************
// Copyright (c) 2012-2018 Charlie Poole, Rob Prouse
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
using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Abstractions;
using NUnit.TestData;
using NUnit.TestUtilities;

namespace NUnit.Framework.Attributes
{
    [NonParallelizable]
    public class TimeoutTests : ThreadingTests
    {
        private static bool _testRanToCompletion;

        [TearDown]
        public void ResetTestCompletionFlag()
        {
            _testRanToCompletion = false;
        }

        private class SampleTests
        {
            private const int TimeExceedingTimeout = 200;

            public const int Timeout = 100;
            public const string FailureMessage = "The test has failed";

            [Timeout(Timeout)]
            public void TestThatTimesOutButOtherwisePasses()
            {
                Thread.Sleep(TimeExceedingTimeout);
                _testRanToCompletion = true;
                Assert.Pass();
            }

            [Timeout(Timeout)]
            public void TestThatTimesOutAndFails()
            {
                Thread.Sleep(TimeExceedingTimeout);
                _testRanToCompletion = true;
                Assert.Fail(FailureMessage);
            }

            [Timeout(Timeout)]
            public void TestThatTimesOutAndThrows()
            {
                Thread.Sleep(TimeExceedingTimeout);
                _testRanToCompletion = true;
                throw new Exception();
            }

            [Timeout(Timeout)]
            public void TestThatThrowsImmediately()
            {
                throw new Exception();
            }

            [Timeout(Timeout)]
            public void TestThatPassesImmediately()
            {
                 Assert.Pass();
            }

            [Timeout(Timeout)]
            public void TestThatFailsImmediately()
            {
                Assert.Fail(FailureMessage);
            }
        }

        [Test, Timeout(500), SetCulture("fr-BE"), SetUICulture("es-BO")]
        public void TestWithTimeoutRespectsCulture()
        {
            Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo("fr-BE"));
            Assert.That(CultureInfo.CurrentUICulture.Name, Is.EqualTo("es-BO"));
        }

        [Test, Timeout(500)]
        public void TestWithTimeoutCurrentContextIsNotAnAdhocContext()
        {
            Assert.That(TestExecutionContext.CurrentContext, Is.Not.TypeOf<TestExecutionContext.AdhocContext>());
        }

        [Test]
        public void TimeoutIsNotEnforcedAndOriginalFailureCauseIsReportedForFailingTestWithDebuggerAttached()
        {
            // given
            var testThatTimesOutAndFails =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutAndFails));

            var attachedDebugger = new StubDebugger { IsAttached = true };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutAndFails, new SampleTests(), attachedDebugger);

            // then
            Assert.That(_testRanToCompletion, () => "Test did not run to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.Message, Is.EqualTo(SampleTests.FailureMessage));
        }

        [Test]
        public void TimeoutIsNotEnforcedAndErrorIsReportedForTestFailingOnExceptionWithDebuggerAttached()
        {
            // given
            var testThatTimesOutAndThrows =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutAndThrows));

            var attachedDebugger = new StubDebugger { IsAttached = true };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutAndThrows, new SampleTests(), attachedDebugger);

            // then
            Assert.That(_testRanToCompletion, () => "Test did not run to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.ResultState.Label, Is.EqualTo(ResultState.Error.Label));
        }

        [Test]
        [Theory]
        public void ErrorIsReportedForTestThatFailsWithoutTimingOut(bool debuggerAttached)
        {
            // given
            var testThatFailsImmediately =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatFailsImmediately));

            var debugger = new StubDebugger { IsAttached = debuggerAttached };

            // when
            var result = TestBuilder.RunTest(testThatFailsImmediately, new SampleTests(), debugger);

            // then
            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.Message, Is.EqualTo(SampleTests.FailureMessage));
        }

        [Test]
        [Theory]
        public void ErrorIsReportedForTestThatThrowsWithoutTimingOut(bool debuggerAttached)
        {
            // given
            var testThatThrowsImmediately =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatThrowsImmediately));

            var debugger = new StubDebugger { IsAttached = debuggerAttached };

            // when
            var result = TestBuilder.RunTest(testThatThrowsImmediately, new SampleTests(), debugger);

            // then
            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.ResultState.Label, Is.EqualTo(ResultState.Error.Label));
        }

        [Test]
        [Theory]
        public void SuccessIsReportedForTestThatPassesWithoutTimingOut(bool debuggerAttached)
        {
            // given
            var testThatPassesImmediately =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatPassesImmediately));

            var debugger = new StubDebugger { IsAttached = debuggerAttached };

            // when
            var result = TestBuilder.RunTest(testThatPassesImmediately, new SampleTests(), debugger);

            // then
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Success));
        }

#if PLATFORM_DETECTION && THREAD_ABORT
        [Test, Timeout(500)]
        public void TestWithTimeoutRunsOnSameThread()
        {
            Assert.That(Thread.CurrentThread, Is.EqualTo(ParentThread));
        }

        [Test, Timeout(500)]
        public void TestWithTimeoutRunsSetUpAndTestOnSameThread()
        {
            Assert.That(Thread.CurrentThread, Is.EqualTo(SetupThread));
        }

        [Test]
        public void TestTimesOutAndTearDownIsRun()
        {
            TimeoutFixture fixture = new TimeoutFixture();
            TestSuite suite = TestBuilder.MakeFixture(fixture);
            TestMethod testMethod = (TestMethod)TestFinder.Find("InfiniteLoopWith50msTimeout", suite, false);
            ITestResult result = TestBuilder.RunTest(testMethod, fixture);
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Does.Contain("50ms"));
            Assert.That(fixture.TearDownWasRun, "TearDown was not run");
        }

        [Test]
        public void SetUpTimesOutAndTearDownIsRun()
        {
            TimeoutFixture fixture = new TimeoutFixtureWithTimeoutInSetUp();
            TestSuite suite = TestBuilder.MakeFixture(fixture);
            TestMethod testMethod = (TestMethod)TestFinder.Find("Test1", suite, false);
            ITestResult result = TestBuilder.RunTest(testMethod, fixture);
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Does.Contain("50ms"));
            Assert.That(fixture.TearDownWasRun, "TearDown was not run");
        }

        [Test]
        public void TearDownTimesOutAndNoFurtherTearDownIsRun()
        {
            TimeoutFixture fixture = new TimeoutFixtureWithTimeoutInTearDown();
            TestSuite suite = TestBuilder.MakeFixture(fixture);
            TestMethod testMethod = (TestMethod)TestFinder.Find("Test1", suite, false);
            ITestResult result = TestBuilder.RunTest(testMethod, fixture);
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Does.Contain("50ms"));
            Assert.That(fixture.TearDownWasRun, "Base TearDown should not have been run but was");
        }

        [Test]
        public void TimeoutCanBeSetOnTestFixture()
        {
            ITestResult suiteResult = TestBuilder.RunTestFixture(typeof(TimeoutFixtureWithTimeoutOnFixture));
            Assert.That(suiteResult.ResultState, Is.EqualTo(ResultState.ChildFailure));
            Assert.That(suiteResult.Message, Is.EqualTo(TestResult.CHILD_ERRORS_MESSAGE));
            Assert.That(suiteResult.ResultState.Site, Is.EqualTo(FailureSite.Child));
            ITestResult result = TestFinder.Find("Test2WithInfiniteLoop", suiteResult, false);
            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Does.Contain("50ms"));
        }

        [Test]
        public void TimeoutCausesOtherwisePassingTestToFail()
        {
            // given
            var testThatTimesOutButOtherwisePasses =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutButOtherwisePasses));

            var detachedDebugger = new StubDebugger { IsAttached = false };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutButOtherwisePasses, new SampleTests(), detachedDebugger);

            // then
            Assert.That(_testRanToCompletion == false, () => "Test ran to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.Message, Is.EqualTo($"Test exceeded Timeout value of {SampleTests.Timeout}ms"));
        }

        [Test]
        public void TimeoutIsNotEnforcedButStillCausesTestFailureWithDebuggerAttached()
        {
            // given
            var testThatTimesOutButOtherwisePasses =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutButOtherwisePasses));

            var attachedDebugger = new StubDebugger { IsAttached = true };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutButOtherwisePasses, new SampleTests(), attachedDebugger);

            // then
            Assert.That(_testRanToCompletion, () => "Test did not run to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.Message, Is.EqualTo($"Test exceeded Timeout value of {SampleTests.Timeout}ms"));
        }

        [Explicit("Tests that demonstrate Timeout failure")]
        public class ExplicitTests
        {
            [Test, Timeout(50)]
            public void TestTimesOut()
            {
                while (true) ;
            }

            [Test, Timeout(50), RequiresThread]
            public void TestTimesOutUsingRequiresThread()
            {
                while (true) ;
            }

            [Test, Timeout(50), Apartment(ApartmentState.STA)]
            public void TestTimesOutInSTA()
            {
                while (true) ;
            }

            // TODO: The test in TimeoutTestCaseFixture work as expected when run
            // directly by NUnit. It's only when run via TestBuilder as a second
            // level test that the result is incorrect. We need to fix this.
            [Test]
            public void TestTimeOutTestCaseWithOutElapsed()
            {
                TimeoutTestCaseFixture fixture = new TimeoutTestCaseFixture();
                TestSuite suite = TestBuilder.MakeFixture(fixture);
                ParameterizedMethodSuite methodSuite = (ParameterizedMethodSuite)TestFinder.Find("TestTimeOutTestCase", suite, false);
                ITestResult result = TestBuilder.RunTest(methodSuite, fixture);
                Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure), "Suite result");
                Assert.That(result.Children.ToArray()[0].ResultState, Is.EqualTo(ResultState.Success), "First test");
                Assert.That(result.Children.ToArray()[1].ResultState, Is.EqualTo(ResultState.Failure), "Second test");
            }
        }

        [Test, Platform("Win")]
        public void TimeoutWithMessagePumpShouldAbort()
        {
            ITestResult result = TestBuilder.RunTest(
                TestBuilder.MakeTestFromMethod(typeof(TimeoutFixture), nameof(TimeoutFixture.TimeoutWithMessagePumpShouldAbort)),
                new TimeoutFixture());

            Assert.That(result.ResultState, Is.EqualTo(ResultState.Failure));
            Assert.That(result.Message, Is.EqualTo("Test exceeded Timeout value of 500ms"));
        }
#endif

#if !THREAD_ABORT
        [Test]
        public void TimeoutCausesOtherwisePassingTestToFail()
        {
            // given
            var testThatTimesOutButOtherwisePasses =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutButOtherwisePasses));

            var detachedDebugger = new StubDebugger { IsAttached = false };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutButOtherwisePasses, new SampleTests(), detachedDebugger);

            // then
            Assert.That(_testRanToCompletion == false, () => "Test ran to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.ResultState.Label, Is.EqualTo($"Test exceeded Timeout value {SampleTests.Timeout}ms."));
        }

        [Test]
        public void TimeoutIsNotEnforcedButStillCausesTestFailureWithDebuggerAttached()
        {
            // given
            var testThatTimesOutButOtherwisePasses =
                TestBuilder.MakeTestCase(typeof(SampleTests), nameof(SampleTests.TestThatTimesOutButOtherwisePasses));

            var attachedDebugger = new StubDebugger { IsAttached = true };

            // when
            var result = TestBuilder.RunTest(testThatTimesOutButOtherwisePasses, new SampleTests(), attachedDebugger);

            // then
            Assert.That(_testRanToCompletion, () => "Test did not run to completion");

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
            Assert.That(result.ResultState.Site, Is.EqualTo(FailureSite.Test));
            Assert.That(result.ResultState.Label, Is.EqualTo($"Test exceeded Timeout value {SampleTests.Timeout}ms."));
        }
#endif

        private class StubDebugger : IDebugger
        {
            public bool IsAttached { get; set; }
        }
    }
}
