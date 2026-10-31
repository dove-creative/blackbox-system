using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    [NonParallelizable]
    [UniTestProject]
    internal sealed class BlackboxSessionUniTests : BlackboxTestBase
    {
        // Content
        // Session contracts: execute and assert every transition in each deterministic lifetime chain.
        [RunWithUniTest]
        [TestCase("NestedInnerFirst")]
        [TestCase("NestedOuterFirst")]
        [TestCase("DefaultRestart")]
        [TestCase("ActiveRegistry")]
        [TestCase("ExportFreeze")]
        [TestCase("ParallelIsolation")]
        public async Task SessionTransitionsExecuteEveryStep(string scenario)
        {
            var project = new SessionTransitionProject(CreateTransitions(scenario));
            try
            {
                var result = await project.Execute(project.StepIds.Length);
                Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetFailedReports().ToJson());
                Assert.That(cancelled, Is.False);

                var leaf = result;
                while (leaf.Afters.Count > 0)
                {
                    Assert.That(leaf.Afters.Count, Is.EqualTo(1), "A session transition chain must not branch.");
                    leaf = leaf.Afters.Single();
                }

                Assert.That(leaf.Model.GetExecutionHistory().Split(Model.Separator), Is.EqualTo(project.StepIds));
                Assert.That(leaf.Model.ExecutionCount, Is.EqualTo(project.StepIds.Length));
                Assert.That(leaf.Model.Completed, Is.True, "The final cleanup transition must execute.");
            }
            finally
            {
                // A failing prefix can own sessions even when UniTest cannot append its next node.
                BlackboxHandle.DisposeActiveSessions();
                BlackboxTestDoubles.CleanupTempDirectories();
                BlackboxHandle.RestartDefaultSession();
            }
        }

        private static Transition[] CreateTransitions(string scenario)
        {
            var transitions = new List<Transition>
            {
                Step("Initialize", m => m.Initialize(), m =>
                {
                    Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey));
                    Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                    Assert.That(m.DefaultHandle.IsValid, Is.True);
                }),
                BeginOuter()
            };

            switch (scenario)
            {
                case "NestedInnerFirst":
                    transitions.AddRange(NestedTransitions(false));
                    break;
                case "NestedOuterFirst":
                    transitions.AddRange(NestedTransitions(true));
                    break;
                case "DefaultRestart":
                    transitions.AddRange(DefaultRestartTransitions());
                    break;
                case "ActiveRegistry":
                    transitions.AddRange(ActiveRegistryTransitions());
                    break;
                case "ExportFreeze":
                    transitions.AddRange(ExportFreezeTransitions());
                    break;
                case "ParallelIsolation":
                    transitions.AddRange(ParallelTransitions());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown session transition chain.");
            }

            transitions.Add(Step("Complete", m =>
            {
                BlackboxHandle.DisposeActiveSessions();
                m.Inner?.Dispose();
                m.Outer.Dispose();
                m.Completed = true;
            }, m =>
            {
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(BlackboxHandle.DefaultSessionKey));
            }));
            return transitions.ToArray();
        }

        private static Transition BeginOuter() => Step("BeginOuter", m =>
        {
            m.Outer = BlackboxHandle.BeginSession();
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(23));
            BlackboxHandle.MaxLogCount = 17;
            m.OuterHandle = BlackboxHandle.Of(m.Owner, label: "outer");
            m.OuterHandle.Write("OUTER_RECORD");
        }, m =>
        {
            // Session contract: copied settings, distinct identity, and independent object metadata.
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Outer.SessionKey));
            Assert.That(m.Outer.SessionKey, Is.Not.EqualTo(m.DefaultKey));
            Assert.That(m.OuterHandle.OwnerString, Is.EqualTo("outer"));
            Assert.That(m.DefaultHandle.OwnerString, Is.EqualTo("default"));
            Assert.That(m.DefaultHandle.SessionKey, Is.EqualTo(m.DefaultKey));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { m.Outer.SessionKey }));
        });

        private static Transition BeginInner() => Step("BeginInner", m =>
        {
            m.Inner = BlackboxHandle.BeginSession();
            Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(17));
            BlackboxHandle.MaxLogCount = 9;
            m.InnerHandle = BlackboxHandle.Of(m.Owner, label: "inner");
            m.InnerHandle.Write("INNER_RECORD");
            m.OuterHandle.Write("RETAINED_OUTER_RECORD");
        }, m =>
        {
            Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Inner.SessionKey));
            Assert.That(m.Inner.SessionKey, Is.Not.EqualTo(m.Outer.SessionKey));
            Assert.That(m.InnerHandle.OwnerString, Is.EqualTo("inner"));
            Assert.That(m.OuterHandle.OwnerString, Is.EqualTo("outer"));
            Assert.That(m.OuterHandle.SessionKey, Is.EqualTo(m.Outer.SessionKey));
            Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { m.Outer.SessionKey, m.Inner.SessionKey }));
        });

        private static IEnumerable<Transition> NestedTransitions(bool disposeOuterFirst)
        {
            // Sessions / Nested lifetime x Dispose: restore the exact previous binding, including a disposed parent.
            yield return BeginInner();
            if (disposeOuterFirst)
            {
                yield return Step("DisposeOuterBeforeInner", m => m.Outer.Dispose(), m =>
                {
                    Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Inner.SessionKey));
                    Assert.That(m.OuterHandle.IsValid, Is.False);
                    Assert.That(m.OuterHandle.SessionKey, Is.EqualTo(Guid.Empty));
                    Assert.That(m.InnerHandle.IsValid, Is.True);
                    Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { m.Inner.SessionKey }));
                });
            }

            yield return Step("DisposeInnerAndRestoreParent", m => m.Inner.Dispose(), m =>
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Outer.SessionKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(17));
                Assert.That(m.InnerHandle.IsValid, Is.False);
                Assert.That(BlackboxHandle.Of(m.Owner).IsValid, Is.EqualTo(!disposeOuterFirst));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.Not.EqualTo(m.DefaultKey));
            });
            yield return Step("DisposeParentAndRestoreDefault", m =>
            {
                m.Inner.Dispose();
                m.Outer.Dispose();
                m.Outer.Dispose();
                m.OuterHandle.Write("REJECTED_OUTER_RECORD");
                m.InnerHandle.Write("REJECTED_INNER_RECORD");
            }, m =>
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(23));
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(m.DefaultHandle.IsValid, Is.True);
                Assert.That(m.ReadExport(m.OuterHandle), Does.Contain("OUTER_RECORD")
                    .And.Contain("RETAINED_OUTER_RECORD").And.Not.Contain("INNER_RECORD").And.Not.Contain("REJECTED"));
                Assert.That(m.ReadExport(m.InnerHandle), Does.Contain("INNER_RECORD")
                    .And.Not.Contain("OUTER_RECORD").And.Not.Contain("REJECTED"));
            });
        }

        private static IEnumerable<Transition> DefaultRestartTransitions()
        {
            // Sessions / Explicit lifetime x RestartDefaultSession: preserve explicit settings, retire only the old default.
            yield return BeginInner();
            yield return Step("RestartDefaultInsideNestedSession", m =>
            {
                BlackboxHandle.RestartDefaultSession();
                m.ReplacementKey = BlackboxHandle.DefaultSessionKey;
                m.DefaultHandle.Write("REJECTED_OLD_DEFAULT");
                m.InnerHandle.Write("INNER_AFTER_RESTART");
            }, m =>
            {
                // Session contract: replacing the default does not retire explicit sessions or copy their settings.
                Assert.That(m.ReplacementKey, Is.Not.EqualTo(m.DefaultKey));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Inner.SessionKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(9));
                Assert.That(m.DefaultHandle.IsValid, Is.False);
                Assert.That(m.OuterHandle.IsValid && m.InnerHandle.IsValid, Is.True);
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(2));
            });
            yield return Step("RestoreReplacementDefault", m =>
            {
                m.Inner.Dispose();
                m.Outer.Dispose();
                m.FreshHandle = BlackboxHandle.Of(m.Owner, label: "replacement");
                m.FreshHandle.Write("NEW_DEFAULT_RECORD");
            }, m =>
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.ReplacementKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(23));
                Assert.That(m.FreshHandle.Id, Is.Zero);
                Assert.That(m.FreshHandle.OwnerString, Is.EqualTo("replacement"));
                Assert.That(m.ReadExport(m.DefaultHandle), Does.Contain("DEFAULT_RECORD")
                    .And.Not.Contain("NEW_DEFAULT_RECORD").And.Not.Contain("REJECTED"));
                Assert.That(m.ReadExport(m.FreshHandle), Does.Contain("NEW_DEFAULT_RECORD").And.Not.Contain("OUTER_RECORD"));
                Assert.That(m.ReadExport(m.InnerHandle), Does.Contain("INNER_AFTER_RESTART"));
            });
        }

        private static IEnumerable<Transition> ActiveRegistryTransitions()
        {
            // Sessions / Active registry x Snapshot and BulkDispose: preserve snapshots and reclaim outstanding handles.
            yield return Step("CaptureActiveSnapshot", m =>
            {
                m.Snapshot = BlackboxHandle.GetActiveSessionKeys();
                m.OuterScope = m.OuterHandle.Scope("outer open scope");
                m.OuterExert = m.OuterHandle.Exert(m.Peer, "outer interaction");
            }, m => Assert.That(m.Snapshot, Is.EqualTo(new[] { m.Outer.SessionKey })));
            yield return BeginInner();
            yield return Step("SnapshotRemainsIndependent", m =>
            {
                Assert.That(m.Snapshot, Is.EqualTo(new[] { m.Outer.SessionKey }));
                m.Snapshot[0] = Guid.Empty;
                m.InnerScope = m.InnerHandle.Scope("inner open scope");
                m.InnerExert = m.InnerHandle.Exert(m.Peer, "inner interaction");
            }, m =>
            {
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { m.Outer.SessionKey, m.Inner.SessionKey }));
                Assert.That(m.OuterScope.IsAlive && m.InnerScope.IsAlive && m.OuterExert.IsAlive && m.InnerExert.IsAlive, Is.True);
            });
            yield return Step("BulkDisposeExplicitSessions", m =>
            {
                m.Snapshot = BlackboxHandle.GetActiveSessionKeys();
                BlackboxHandle.DisposeActiveSessions();
            }, m =>
            {
                // Active session contract: reverse-order cleanup restores only this flow and excludes the default.
                Assert.That(m.Snapshot, Is.EqualTo(new[] { m.Outer.SessionKey, m.Inner.SessionKey }));
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.Empty);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey));
                Assert.That(m.OuterHandle.IsValid || m.InnerHandle.IsValid, Is.False);
                Assert.That(m.OuterScope.IsAlive || m.InnerScope.IsAlive || m.OuterExert.IsAlive || m.InnerExert.IsAlive, Is.False);
                Assert.That(m.DefaultHandle.IsValid, Is.True);
            });
            yield return Step("RepeatCleanupAndStartNewLifetime", m =>
            {
                BlackboxHandle.DisposeActiveSessions();
                m.OuterScope.Dispose();
                m.InnerScope.Dispose();
                m.OuterExert.Dispose();
                m.InnerExert.Dispose();
                m.Inner.Dispose();
                m.Outer.Dispose();
                using var next = BlackboxHandle.BeginSession();
                m.FreshHandle = BlackboxHandle.Of(m.Owner);
                Assert.That(m.FreshHandle.IsValid, Is.True);
                Assert.That(next.SessionKey, Is.Not.EqualTo(m.Inner.SessionKey).And.Not.EqualTo(m.Outer.SessionKey));
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { next.SessionKey }));
            }, m =>
            {
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey));
                Assert.That(m.FreshHandle.IsValid, Is.False);
            });
        }

        private static IEnumerable<Transition> ExportFreezeTransitions()
        {
            // Sessions / Active, Frozen, Disposed x Export: distinguish output failure from recording retirement.
            yield return Step("ExportWhileRecording", m =>
            {
                Assert.That(m.ReadExport(m.OuterHandle), Does.Contain("OUTER_RECORD"));
                m.OuterHandle.Write("AFTER_FIRST_EXPORT");
            }, m =>
            {
                Assert.That(m.OuterHandle.IsValid, Is.True);
                Assert.That(m.ReadExport(m.OuterHandle), Does.Contain("AFTER_FIRST_EXPORT"));
                Assert.That(Directory.GetFiles(m.Directory).Length, Is.EqualTo(2));
            });
            yield return Step("OrdinaryExportFailureKeepsRecording", m =>
            {
                BlackboxHandle.LogDirectory = null;
                Assert.Throws<InvalidOperationException>(() => m.OuterHandle.Export());
                m.OuterHandle.Write("AFTER_FAILED_EXPORT");
            }, m => Assert.That(m.OuterHandle.IsValid, Is.True));
            yield return Step("CrashOutputFailureStillFreezes", m =>
            {
                Assert.Throws<InvalidOperationException>(() => m.OuterHandle.CrashExport("FIRST_CRASH", openLog: OpenLogOption.Never));
                m.OuterHandle.Write("REJECTED_AFTER_CRASH");
            }, m =>
            {
                Assert.That(m.OuterHandle.IsValid, Is.False);
                Assert.That(m.OuterHandle.SessionKey, Is.EqualTo(Guid.Empty));
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Outer.SessionKey));
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(1));
                Assert.That(m.DefaultHandle.IsValid, Is.True);
            });
            yield return Step("RetryFrozenSnapshotWithOrdinaryExport", m =>
            {
                BlackboxHandle.LogDirectory = m.Directory;
                var report = m.ReadExport(m.OuterHandle);
                Assert.That(report, Does.Contain("FIRST_CRASH").And.Contain("AFTER_FAILED_EXPORT")
                    .And.Contain("AFTER_FIRST_EXPORT").And.Not.Contain("REJECTED_AFTER_CRASH"));
                var before = Directory.GetFiles(m.Directory).Length;
                Assert.That(m.OuterHandle.CrashExport("SECOND_CRASH"), Is.EqualTo("SECOND_CRASH"));
                Assert.That(Directory.GetFiles(m.Directory).Length, Is.EqualTo(before));
            }, m =>
            {
                Assert.That(m.ReadExport(m.OuterHandle), Does.Contain("FIRST_CRASH").And.Not.Contain("SECOND_CRASH"));
                Assert.That(m.OuterHandle.IsValid, Is.False);
            });
            yield return Step("DisposeFrozenSessionAndStartFresh", m =>
            {
                m.Outer.Dispose();
                using var next = BlackboxHandle.BeginSession();
                var fresh = BlackboxHandle.Of(m.Owner);
                fresh.Write("FRESH_SESSION_RECORD");
                Assert.That(fresh.IsValid, Is.True);
                Assert.That(m.ReadExport(fresh), Does.Contain("FRESH_SESSION_RECORD").And.Not.Contain("FIRST_CRASH"));
            }, m => Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey)));
        }

        private static IEnumerable<Transition> ParallelTransitions()
        {
            // Sessions / Parallel async children x Record and Crash: inherit settings without sharing writable state.
            yield return Step("RunParallelInheritedChildren", m => m.RunParallelChildren(), m =>
            {
                // Async session contract: settings, crash state and object storage belong to each child flow.
                Assert.That(m.Workers.Select(worker => worker.Key).Distinct().Count(), Is.EqualTo(2));
                Assert.That(m.Workers.Select(worker => worker.Key), Has.None.EqualTo(m.Outer.SessionKey));
                Assert.That(m.Workers[0].WasValidAfterRecording, Is.False);
                Assert.That(m.Workers[1].WasValidAfterRecording, Is.True);
                Assert.That(m.Workers.All(worker => !worker.Handle.IsValid), Is.True);
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.Outer.SessionKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(17));
                Assert.That(m.OuterHandle.IsValid && m.DefaultHandle.IsValid, Is.True);
                Assert.That(BlackboxHandle.GetActiveSessionKeys(), Is.EqualTo(new[] { m.Outer.SessionKey }));
                Assert.That(m.ReadExport(m.Workers[0].Handle), Does.Contain("WORKER_ZERO")
                    .And.Contain("CHILD_CRASH").And.Not.Contain("WORKER_ONE"));
                Assert.That(m.ReadExport(m.Workers[1].Handle), Does.Contain("WORKER_ONE")
                    .And.Not.Contain("WORKER_ZERO").And.Not.Contain("CHILD_CRASH"));
            });
            yield return Step("ParentRemainsWritable", m =>
            {
                m.OuterHandle.Write("PARENT_AFTER_CHILDREN");
                Assert.That(m.ReadExport(m.OuterHandle), Does.Contain("PARENT_AFTER_CHILDREN")
                    .And.Not.Contain("WORKER_ZERO").And.Not.Contain("WORKER_ONE"));
                m.Outer.Dispose();
            }, m =>
            {
                Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(m.DefaultKey));
                Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(23));
                Assert.That(BlackboxHandle.ActiveSessionCount, Is.Zero);
            });
        }

        private static Transition Step(string id, Action<SessionModel> actor, Action<SessionModel> asserter) =>
            new Transition(id, actor, asserter);

        private sealed class Transition
        {
            public readonly string Id;
            public readonly Action<SessionModel> Actor;
            public readonly Action<SessionModel> Asserter;

            public Transition(string id, Action<SessionModel> actor, Action<SessionModel> asserter)
            {
                Id = id;
                Actor = actor;
                Asserter = asserter;
            }
        }

        private sealed class SessionTransitionProject : Project<SessionModel>
        {
            // Front
            public string[] StepIds => _steps.Select(step => step.Id).ToArray();

            // Internal
            private readonly Transition[] _steps;


            // Content
            public SessionTransitionProject(Transition[] steps)
                : base(ExecutionMode.Inline) => _steps = steps;

            public override IEnumerable<ILab<SessionModel>> CreateLabs(SessionModel model)
            {
                if (model.ExecutionCount >= _steps.Length)
                    return Array.Empty<ILab<SessionModel>>();

                var step = _steps[model.ExecutionCount];
                return new[]
                {
                    new Lab<SessionModel>(step.Id,
                        actor: (current, _) => current.InContext(step.Actor),
                        asserter: (current, _) => current.InContext(step.Asserter))
                };
            }
        }

        private sealed class SessionModel : Model
        {
            // Front
            public readonly object Owner = new object();
            public readonly object Peer = new object();
            public readonly ConcurrentQueue<string> Warnings = new ConcurrentQueue<string>();
            public BlackboxSession Outer;
            public BlackboxSession Inner;
            public BlackboxHandle DefaultHandle;
            public BlackboxHandle OuterHandle;
            public BlackboxHandle InnerHandle;
            public BlackboxHandle FreshHandle;
            public ScopeHandle OuterScope;
            public ScopeHandle InnerScope;
            public ExertHandle OuterExert;
            public ExertHandle InnerExert;
            public Guid DefaultKey;
            public Guid ReplacementKey;
            public Guid[] Snapshot;
            public WorkerResult[] Workers;
            public string Directory;
            public bool Completed;

            // Internal
            private ExecutionContext _context;


            // Content
            public SessionModel() { }

            public void InContext(Action<SessionModel> action)
            {
                // UniTest replays ancestors and schedules each Lab separately. Preserve this model's logical flow.
                var context = _context ?? ExecutionContext.Capture();
                ExecutionContext.Run(context, _ =>
                {
                    try { action(this); }
                    finally { _context = ExecutionContext.Capture(); }
                }, null);
            }

            public void Initialize()
            {
                // Replay creates a fresh model; retire the previous prefix before reproducing its transitions.
                BlackboxHandle.DisposeActiveSessions();
                BlackboxTestDoubles.CleanupTempDirectories();
                BlackboxTestDoubles.Reset();
                Directory = BlackboxTestDoubles.CreateTempDirectory();
                BlackboxHandle.Configure(Directory, _ => { }, Warnings.Enqueue,
                    exportFormat: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                BlackboxHandle.MaxLogCount = 23;
                DefaultKey = BlackboxHandle.DefaultSessionKey;
                DefaultHandle = BlackboxHandle.Of(Owner, label: "default");
                DefaultHandle.Write("DEFAULT_RECORD");
                Subject = Owner;
            }

            public string ReadExport(BlackboxHandle handle)
            {
                var previous = new HashSet<string>(System.IO.Directory.GetFiles(Directory));
                handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                var path = System.IO.Directory.GetFiles(Directory).Single(file => !previous.Contains(file));
                return File.ReadAllText(path);
            }

            public void RunParallelChildren()
            {
                var entered = 0;
                var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var tasks = new[] { RunWorker(0), RunWorker(1) };
                Exception failure = null;

                try
                {
                    ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                    Assert.That(BlackboxHandle.ActiveSessionCount, Is.EqualTo(3));
                }
                catch (Exception exception) { failure = exception; }
                finally { release.TrySetResult(true); }

                try { Workers = Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
                catch (Exception exception)
                {
                    failure = failure == null ? exception : new AggregateException(failure, exception);
                }
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();

                Task<WorkerResult> RunWorker(int index) => Task.Run(async () =>
                {
                    using var session = BlackboxHandle.BeginSession();
                    Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(17));
                    BlackboxHandle.MaxLogCount = index == 0 ? 5 : 8;
                    var handle = BlackboxHandle.Of(Owner, label: "worker-" + index);
                    if (Interlocked.Increment(ref entered) == 2) ready.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    await Task.Yield();
                    Assert.That(BlackboxHandle.CurrentSessionKey, Is.EqualTo(session.SessionKey));
                    Assert.That(BlackboxHandle.MaxLogCount, Is.EqualTo(index == 0 ? 5 : 8));
                    handle.Write(index == 0 ? "WORKER_ZERO" : "WORKER_ONE");
                    if (index == 0)
                        handle.CrashExport("CHILD_CRASH", format: ExportFormat.Txt, openLog: OpenLogOption.Never);
                    return new WorkerResult(session.SessionKey, handle, handle.IsValid);
                });
            }
        }

        private sealed class WorkerResult
        {
            public readonly Guid Key;
            public readonly BlackboxHandle Handle;
            public readonly bool WasValidAfterRecording;

            public WorkerResult(Guid key, BlackboxHandle handle, bool wasValidAfterRecording)
            {
                Key = key;
                Handle = handle;
                WasValidAfterRecording = wasValidAfterRecording;
            }
        }
    }
}
