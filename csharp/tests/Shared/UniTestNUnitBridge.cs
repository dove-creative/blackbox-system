using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace BlackThunder.BlackboxSystem.Tests
{
    // Marks fixtures which also build multi-step UniTest projects.
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class UniTestProjectAttribute : Attribute { }

    // NUnit keeps discovery, arguments and fixture lifetime; UniTest executes each assertion body.
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    internal sealed class RunWithUniTestAttribute : NUnitAttribute, IWrapTestMethod
    {
        public TestCommand Wrap(TestCommand command) => new UniTestCommand(command);

        private sealed class UniTestCommand : DelegatingTestCommand
        {
            public UniTestCommand(TestCommand command)
                : base(command) { }

            public override TestResult Execute(TestExecutionContext context)
            {
                ExceptionDispatchInfo originalFailure = null;
                var invocationCount = 0;
                // Lab IDs cannot contain the execution-path separator. The full NUnit name remains metadata.
                var id = context.CurrentTest.FullName.Replace(Model.Separator, "∕");
                var node = new Node<Model>(new Lab<Model>(id, actor: (model, _) =>
                {
                    model.Subject = context.CurrentTest.FullName;
                    invocationCount++;
                    try
                    {
                        context.CurrentResult = innerCommand.Execute(context);
                        if (context.CurrentResult.ResultState.Status == TestStatus.Failed)
                            throw new AssertionException(context.CurrentResult.Message);
                    }
                    catch (SuccessException success)
                    {
                        context.CurrentResult.SetResult(ResultState.Success, success.Message);
                    }
                    catch (Exception exception)
                    {
                        originalFailure = ExceptionDispatchInfo.Capture(exception);
                        throw;
                    }
                }));

                // A single Node executes synchronously: no replay or extra thread changes the test's behavior.
                node.Execute();
                context.CurrentTest.Properties.Set("UniTest.ExecutionCount", node.Model.ExecutionCount);
                context.CurrentTest.Properties.Set("UniTest.Status", node.Status.ToString());
                if (node.Status != Node<Model>.NodeStatus.Success)
                {
                    context.OutWriter.WriteLine(node.GetFailedReports().ToJson());
                    originalFailure?.Throw();
                    throw new AssertionException(node.GetFailedReports().ToJson());
                }

                if (invocationCount != 1 || node.Model.ExecutionCount != 1)
                    throw new AssertionException("Each NUnit case must execute exactly once through UniTest.");
                return context.CurrentResult;
            }
        }
    }

    [NonParallelizable]
    internal class UniTestIntegrationTests
    {
        [Test, RunWithUniTest]
        public void EveryDiscoveredTestMethodUsesUniTest()
        {
            var missing = GetType().Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Where(method => method.DeclaringType == type))
                .Where(method => method.GetCustomAttributes(true)
                    .Any(attribute => attribute is ITestBuilder || attribute is ISimpleTestBuilder))
                .Where(method => !method.IsDefined(typeof(RunWithUniTestAttribute), true))
                .Select(method => method.DeclaringType.FullName + "." + method.Name)
                .OrderBy(name => name).ToArray();
            Assert.That(missing, Is.Empty, "Add RunWithUniTest so new regression cases cannot bypass UniTest.");
        }

        [Test, RunWithUniTest]
        public void BridgeInvokesBodyOnceAndKeepsTheCallingThread()
        {
            var count = 0;
            var thread = Environment.CurrentManagedThreadId;
            var command = new ProbeCommand(() =>
            {
                count++;
                Assert.That(Environment.CurrentManagedThreadId, Is.EqualTo(thread));
            });
            using (new TestExecutionContext.IsolatedContext())
            {
                var context = TestExecutionContext.CurrentContext;
                new RunWithUniTestAttribute().Wrap(command).Execute(context);
                Assert.That(context.CurrentTest.Properties.Get("UniTest.Status"), Is.EqualTo("Success"));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.ExecutionCount"), Is.EqualTo(1));
            }
            Assert.That(count, Is.EqualTo(1));
        }

        [Test, RunWithUniTest]
        public void BridgePreservesFailuresAndProducesUniTestDiagnostics()
        {
            var expected = new InvalidOperationException("bridge failure marker");
            using (new TestExecutionContext.IsolatedContext())
            {
                var context = TestExecutionContext.CurrentContext;
                var command = new RunWithUniTestAttribute().Wrap(new ProbeCommand(() => throw expected));
                var actual = Assert.Throws<InvalidOperationException>(() => command.Execute(context));
                Assert.That(actual, Is.SameAs(expected));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.Status"), Is.EqualTo("Failure"));
            }
        }

        [Test, RunWithUniTest]
        public void BridgeRejectsReturnedFailureWithoutAnException()
        {
            var invocations = 0;
            var outerResult = TestExecutionContext.CurrentContext.CurrentResult;
            var outerState = outerResult.ResultState;
            using (new TestExecutionContext.IsolatedContext())
            {
                var context = TestExecutionContext.CurrentContext;
                var command = new RunWithUniTestAttribute().Wrap(new ProbeCommand(
                    () => invocations++, ResultState.Failure, "returned failure marker"));

                var failure = Assert.Throws<AssertionException>(() => command.Execute(context));
                Assert.That(failure.Message, Does.Contain("returned failure marker"));
                Assert.That(context.CurrentResult.ResultState.Status, Is.EqualTo(TestStatus.Failed));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.Status"), Is.EqualTo("Failure"));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.ExecutionCount"), Is.EqualTo(1));
            }

            Assert.That(invocations, Is.EqualTo(1));
            Assert.That(TestExecutionContext.CurrentContext.CurrentResult, Is.SameAs(outerResult));
            Assert.That(outerResult.ResultState, Is.EqualTo(outerState));
        }

        [Test, RunWithUniTest]
        public void BridgeAwaitsNUnitAsyncMethodAndPropagatesItsFailureOnce()
        {
            var fixture = new AsyncFailureProbe();
            var method = new TestMethod(new MethodWrapper(typeof(AsyncFailureProbe), nameof(AsyncFailureProbe.Execute)));
            var outerTest = TestExecutionContext.CurrentContext.CurrentTest;
            var outerResult = TestExecutionContext.CurrentContext.CurrentResult;
            using (new TestExecutionContext.IsolatedContext())
            {
                var context = TestExecutionContext.CurrentContext;
                context.CurrentTest = method;
                context.CurrentResult = method.MakeTestResult();
                context.TestObject = fixture;
                var command = new RunWithUniTestAttribute().Wrap(new TestMethodCommand(method));

                // Exercise NUnit's real async adapter: the failure occurs only after the task resumes.
                var failure = Assert.Catch(() => command.Execute(context));
                Assert.That(failure.GetBaseException(), Is.SameAs(fixture.Failure));
                Assert.That(fixture.Resumed, Is.True);
                Assert.That(fixture.Invocations, Is.EqualTo(1));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.Status"), Is.EqualTo("Failure"));
                Assert.That(context.CurrentTest.Properties.Get("UniTest.ExecutionCount"), Is.EqualTo(1));
            }

            Assert.That(TestExecutionContext.CurrentContext.CurrentTest, Is.SameAs(outerTest));
            Assert.That(TestExecutionContext.CurrentContext.CurrentResult, Is.SameAs(outerResult));
        }

        private sealed class AsyncFailureProbe
        {
            public readonly InvalidOperationException Failure = new InvalidOperationException("awaited failure marker");
            public int Invocations;
            public bool Resumed;

            // No NUnit test attribute: this probe is invoked only through a manually constructed command.
            public async Task Execute()
            {
                Invocations++;
                await Task.Yield();
                Resumed = true;
                throw Failure;
            }
        }

        private sealed class ProbeCommand : TestCommand
        {
            private readonly Action _body;
            private readonly ResultState _resultState;
            private readonly string _message;

            public ProbeCommand(Action body, ResultState resultState = null, string message = null)
                : base(TestExecutionContext.CurrentContext.CurrentTest)
            {
                _body = body;
                _resultState = resultState ?? ResultState.Success;
                _message = message;
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                _body();
                context.CurrentResult.SetResult(_resultState, _message);
                return context.CurrentResult;
            }
        }
    }
}
