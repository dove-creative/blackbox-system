using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BlackThunder.BlackboxSystem.Exporters;
using NUnit.Framework;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal static class BlackboxTableScenarioFactory
    {
        private static readonly string[] ProjectNames =
        {
            "LogData",
            "LogFormatter",
            "WriteHandler",
            "TagHandle",
            "ScopeHandle",
            "ExertHandle",
            "BlackboxRuntime",
            "BlackboxRegistry",
            "Infrastructure",
            "LogContext",
            "Blackbox",
            "BlackboxHandle",
            "ExportTools",
            "Exporter",
            "Integration"
        };

        private static readonly int[] ContinuousSeeds =
        {
            0,
            1,
            20260611
        };

        public static IEnumerable<TestCaseData> TableProjectCases()
        {
            return ProjectNames
                .Select(name => new TestCaseData(name).SetName("BlackboxTable_" + name))
                .ToArray();
        }

        public static IEnumerable<TestCaseData> ContinuousTableProjectCases()
        {
            return ProjectNames
                .SelectMany(name => ContinuousSeeds.Select(seed =>
                    new TestCaseData(name, seed).SetName("BlackboxTable_" + name + "_Seed" + seed)))
                .ToArray();
        }

        public static BlackboxTableProject CreateProject(string name, bool continuous, int continuousSeed = 0)
        {
            return new BlackboxTableProject(name, continuous, continuousSeed, CreateScenarios(name));
        }

        private static IReadOnlyList<TableScenario> CreateScenarios(string name)
        {
            switch (name)
            {
                case "LogData": return LogDataScenarios();
                case "LogFormatter": return LogFormatterScenarios();
                case "WriteHandler": return WriteHandlerScenarios();
                case "TagHandle": return TagHandleScenarios();
                case "ScopeHandle": return ScopeHandleScenarios();
                case "ExertHandle": return ExertHandleScenarios();
                case "BlackboxRuntime": return BlackboxRuntimeScenarios();
                case "BlackboxRegistry": return BlackboxRegistryScenarios();
                case "Infrastructure": return InfrastructureScenarios();
                case "LogContext": return LogContextScenarios();
                case "Blackbox": return BlackboxScenarios();
                case "BlackboxHandle": return BlackboxHandleScenarios();
                case "ExportTools": return ExportToolsScenarios();
                case "Exporter": return ExporterScenarios();
                case "Integration": return IntegrationScenarios();
                default: throw new ArgumentOutOfRangeException(nameof(name), name);
            }
        }

        private static IReadOnlyList<TableScenario> LogDataScenarios()
        {
            return new[]
            {
                Scenario("Value_Create", m =>
                {
                    var owner = m.NewBlackbox("value");
                    var log = new LogData(owner, "value", DateTime.UtcNow, 10, "Run", ScopeType.None, -1, 7, "worker", -1, null, null, null, null);

                    Assert.That(log.Owner, Is.SameAs(owner), "Table 1-1 / Value x Create");
                    Assert.That(log.Message, Is.EqualTo("value"));
                    Assert.That(log.Sequence, Is.EqualTo(10));
                    Assert.That(log.MethodName, Is.EqualTo("Run"));
                    Assert.That(log.ThreadId, Is.EqualTo(7));
                    Assert.That(log.ThreadName, Is.EqualTo("worker"));
                    Assert.That(log.IsValid, Is.True);
                }),
                Scenario("Tagged_Create", m =>
                {
                    var owner = m.NewBlackbox("tagged");
                    var target = m.NewBlackbox("target");
                    var tags = new[] { new LogData.Tag(3, target) };
                    var log = new LogData(owner, "tag %0", DateTime.UtcNow, 11, "Tag", ScopeType.None, -1, 1, "main", 3, null, null, tags, owner, TargetTypes.Name);

                    Assert.That(log.Tags, Is.Not.Null, "Table 1-1 / Tagged x Create");
                    Assert.That(log.Tags.Length, Is.EqualTo(1));
                    Assert.That(log.Tags[0].Target, Is.SameAs(target));
                    Assert.That(log.TaggedBy, Is.SameAs(owner));
                    Assert.That(log.TagTargetTypes, Is.EqualTo(TargetTypes.Name));
                }),
                Scenario("Interaction_Create", m =>
                {
                    var owner = m.NewBlackbox("interaction");
                    var target = m.NewBlackbox("target");
                    var source = m.NewBlackbox("source");
                    var log = new LogData(owner, "call", DateTime.UtcNow, 12, "Exert", ScopeType.None, -1, 1, "main", 5, target, source, null, null);

                    Assert.That(log.InteractionId, Is.EqualTo(5), "Table 1-1 / Interaction x Create");
                    Assert.That(log.ExertingTo, Is.SameAs(target));
                    Assert.That(log.ExertedBy, Is.SameAs(source));
                }),
                Scenario("ToString_RenderTextLine", m =>
                {
                    var owner = m.NewBlackbox("line");
                    var log = new LogData(owner, "hello", DateTime.UtcNow, 13, "Run", ScopeType.Open, 2, 1, "main", -1, null, null, null, null);
                    var line = log.ToString(1);

                    Assert.That(line, Does.Contain("hello"), "Table 1-1 / Value x ToString");
                    Assert.That(line, Does.Contain("<Run>"));
                    Assert.That(line, Does.Contain("[13]"));
                }),
                Scenario("MutateForExport", m =>
                {
                    var owner = m.NewBlackbox("mutate");
                    var target = m.NewBlackbox("target");
                    var log = new LogData(owner, "before", DateTime.UtcNow, 14, "Run", ScopeType.None, -1, 1, "main", -1, null, null, null, null);

                    log.ScopeDepth = 3;
                    log.IsValid = false;
                    log.Message = "after";
                    log.InteractionId = 9;
                    log.ExertingTo = target;

                    Assert.That(log.ScopeDepth, Is.EqualTo(3), "Table 1-1 / MutateForExport");
                    Assert.That(log.IsValid, Is.False);
                    Assert.That(log.Message, Is.EqualTo("after"));
                    Assert.That(log.ExertingTo, Is.SameAs(target));
                })
            };
        }

        private static IReadOnlyList<TableScenario> LogFormatterScenarios()
        {
            return new[]
            {
                Scenario("RenderMessage_Placeholders", m =>
                {
                    var owner = m.NewBlackbox("formatter");
                    var target = m.NewBlackbox("target");
                    var log = new LogData(owner, "hello %0", DateTime.UtcNow, 1, "Run", ScopeType.None, -1, 1, "main", -1, null, null, new[] { new LogData.Tag(4, target) }, null);
                    var rendered = LogFormatter.RenderMessage(log);

                    Assert.That(rendered, Does.Contain("hello"), "Table 1-2 / Ready x RenderMessage");
                    Assert.That(rendered, Does.Contain(target.OwnerString));
                    Assert.That(rendered, Does.Contain("[4]"));
                }),
                Scenario("RenderMessage_UnusedTags", m =>
                {
                    var owner = m.NewBlackbox("formatter");
                    var target = m.NewBlackbox("target");
                    var log = new LogData(owner, "hello", DateTime.UtcNow, 1, "Run", ScopeType.None, -1, 1, "main", -1, null, null, new[] { new LogData.Tag(5, target) }, null);

                    Assert.That(LogFormatter.RenderMessage(log), Does.Contain("tag %0"), "Table 1-2 / Ready x RenderMessage");
                }),
                Scenario("RenderTaggedMessage_TargetTypes", m =>
                {
                    var owner = m.NewBlackbox("formatter");
                    var source = m.NewBlackbox("source");
                    var full = new LogData(owner, "message", DateTime.UtcNow, 1, "Tag", ScopeType.None, -1, 1, "main", 6, null, null, null, source, TargetTypes.Full);
                    var none = new LogData(owner, "message", DateTime.UtcNow, 1, "Tag", ScopeType.None, -1, 1, "main", 6, null, null, null, source, TargetTypes.None);

                    Assert.That(LogFormatter.RenderMessage(full), Does.Contain(source.OwnerString), "Table 1-2 / Ready x TagRef");
                    Assert.That(LogFormatter.RenderMessage(full), Does.Contain("message"));
                    Assert.That(LogFormatter.RenderMessage(none), Is.EqualTo("tagged"));
                }),
                Scenario("RenderTextLine_Metadata", m =>
                {
                    var owner = m.NewBlackbox("formatter");
                    var target = m.NewBlackbox("target");
                    var log = new LogData(owner, "call", DateTime.UtcNow, 20, "Run", ScopeType.Close, 2, 9, "worker", 7, target, null, null, null);
                    var line = LogFormatter.RenderTextLine(log, 2);

                    Assert.That(line, Does.Contain("[20]"), "Table 1-2 / Ready x RenderTextLine");
                    Assert.That(line, Does.Contain("worker"));
                    Assert.That(line, Does.Contain("</Run>"));
                    Assert.That(line, Does.Contain(LogFormatter.Arrow(true, 7)));
                }),
                Scenario("RenderMethodLabel", m =>
                {
                    var owner = m.NewBlackbox("formatter");

                    Assert.That(LogFormatter.RenderMethodLabel(NewFormatterLog(owner, ScopeType.None)), Is.EqualTo("[Run]"), "Table 1-2 / Ready x RenderMethodLabel");
                    Assert.That(LogFormatter.RenderMethodLabel(NewFormatterLog(owner, ScopeType.Open)), Is.EqualTo("<Run>"));
                    Assert.That(LogFormatter.RenderMethodLabel(NewFormatterLog(owner, ScopeType.Close)), Is.EqualTo("</Run>"));
                    Assert.That(LogFormatter.RenderMethodLabel(NewFormatterLog(owner, ScopeType.Step)), Is.EqualTo("<Run />"));
                }),
                Scenario("Arrow_TagRef", m =>
                {
                    var target = m.NewBlackbox("target");

                    Assert.That(LogFormatter.Arrow(true, 3), Is.EqualTo("-[3]->"), "Table 1-2 / Ready x Arrow");
                    Assert.That(LogFormatter.Arrow(false, 3), Is.EqualTo("<-[3]-"));
                    Assert.That(LogFormatter.TagRef(target, 3), Does.Contain("[3]"), "Table 1-2 / Ready x TagRef");
                    Assert.That(LogFormatter.TagRef(null, 3), Is.EqualTo("null"));
                })
            };
        }

        private static IReadOnlyList<TableScenario> WriteHandlerScenarios()
        {
            return new[]
            {
                Scenario("ValidHandle_ConstructAppendGetText", m =>
                {
                    var owner = m.NewOwner("handler");
                    var handle = BlackboxHandle.Of(owner);
                    var handler = new BlackboxHandle.WriteHandler(8, 2, handle, out var shouldLog);

                    handler.AppendLiteral("value ");
                    handler.AppendFormatted(10);
                    handler.AppendLiteral(" ");
                    handler.AppendFormatted<object>(null);
                    var text = handler.GetTextAndClear();
                    handle.Write(text, "Run");

                    Assert.That(shouldLog, Is.True, "Table 1-3 / Active x Construct");
                    Assert.That(text, Is.EqualTo("value 10 null"), "Table 1-3 / Active x AppendLiteral/AppendFormatted/GetTextAndClear");
                    Assert.That(LastLog(owner).Message, Is.EqualTo("value 10 null"));
                }),
                Scenario("InvalidHandle_SkipsFormatting", m =>
                {
                    var handler = new BlackboxHandle.WriteHandler(8, 2, default, out var shouldLog);

                    handler.AppendLiteral("value ");
                    handler.AppendFormatted(10);
                    var text = handler.GetTextAndClear();

                    Assert.That(shouldLog, Is.False, "Table 1-3 / Skipped x Construct");
                    Assert.That(text, Is.EqualTo(string.Empty), "Table 1-3 / Skipped x GetTextAndClear");
                })
            };
        }

        private static IReadOnlyList<TableScenario> TagHandleScenarios()
        {
            return new[]
            {
                Scenario("Default_With_ToString", m =>
                {
                    var result = default(TagHandle).With(m.NewOwner("target"));

                    Assert.That(result, Is.EqualTo(string.Empty), "Table 2-1 / Default x With");
                    Assert.That((string)default(TagHandle), Is.EqualTo(string.Empty), "Table 2-1 / Default x ToString");
                }),
                Scenario("FallbackMessage_ToString", m =>
                {
                    var result = default(BlackboxHandle).Write("fallback", "Run");

                    Assert.That((string)result, Is.EqualTo("fallback"), "Table 2-1 / FallbackMessage x ToString");
                    Assert.That(result.With(m.NewOwner("target")), Is.EqualTo("fallback"));
                }),
                Scenario("SourceLog_WithValidTarget", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var message = BlackboxHandle.Of(source).Write("uses %0", "Run").With(target, TargetTypes.Full);
                    var sourceLog = LastLog(source);
                    var targetLog = LastLog(target);

                    Assert.That(message, Does.Contain("uses"), "Table 2-1 / SourceLog x With.ValidTarget");
                    Assert.That(sourceLog.Tags.Length, Is.EqualTo(1));
                    Assert.That(sourceLog.Tags[0].Target, Is.SameAs(Box(target)));
                    Assert.That(targetLog.IsTagged, Is.True);
                    Assert.That(targetLog.TaggedBy, Is.SameAs(Box(source)));
                }),
                Scenario("SourceLog_WithNullTarget", m =>
                {
                    var source = m.NewOwner("source");
                    var message = BlackboxHandle.Of(source).Write("uses %0", "Run").With((object)null);
                    var sourceLog = LastLog(source);

                    Assert.That(message, Does.Contain("null"), "Table 2-1 / SourceLog x NullTarget");
                    Assert.That(sourceLog.Tags[0].Target, Is.Null);
                }),
                Scenario("SourceLog_WithNullArray", m =>
                {
                    var source = m.NewOwner("source");
                    var message = BlackboxHandle.Of(source).Write("uses %0", "Run").With((object[])null);
                    var sourceLog = LastLog(source);

                    Assert.That(message, Does.Contain("null"), "Table 2-1 / SourceLog x NullArray");
                    Assert.That(sourceLog.Tags[0].Target, Is.Null);
                }),
                Scenario("SourceLog_TargetTypesLast", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    BlackboxHandle.Of(source).Write("uses %0", "Run").With(target, TargetTypes.Name);

                    Assert.That(LastLog(target).TagTargetTypes, Is.EqualTo(TargetTypes.Name), "Table 2-1 / SourceLog x TargetTypesLast");
                })
            };
        }

        private static IReadOnlyList<TableScenario> ScopeHandleScenarios()
        {
            return new[]
            {
                Scenario("Default_NoOps", m =>
                {
                    var before = BlackboxRuntime.CurrentSequence;
                    var handle = default(ScopeHandle).With(m.NewOwner("target"));
                    handle.Dispose();

                    Assert.That(handle.IsDisposed, Is.True, "Table 2-2 / Default x With/Dispose");
                    Assert.That(BlackboxRuntime.CurrentSequence, Is.EqualTo(before));
                }),
                Scenario("Alive_DisposeSameThread", m =>
                {
                    var owner = m.NewOwner("scope");
                    var handle = BlackboxHandle.Of(owner).Scope("open", "Scope");
                    Assert.That(handle.IsAlive, Is.True, "Table 2-2 / Alive x Dispose.SameThread");

                    handle.Dispose();
                    var logs = Logs(owner);

                    Assert.That(handle.IsDisposed, Is.True);
                    Assert.That(logs.Count(log => log.ScopeType == ScopeType.Open), Is.EqualTo(1));
                    Assert.That(logs.Count(log => log.ScopeType == ScopeType.Close), Is.EqualTo(1));
                }, NotPrinted),
                Scenario("Disposed_DisposeAgain", m =>
                {
                    var owner = m.NewOwner("scope");
                    var handle = BlackboxHandle.Of(owner).Scope("open", "Scope");
                    handle.Dispose();
                    var count = Logs(owner).Count;
                    handle.Dispose();

                    Assert.That(Logs(owner).Count, Is.EqualTo(count), "Table 2-2 / Disposed x Dispose");
                }, NotPrinted),
                Scenario("Alive_WithValidTarget", m =>
                {
                    var owner = m.NewOwner("scope");
                    var target = m.NewOwner("target");
                    var handle = BlackboxHandle.Of(owner).Scope("open %0", "Scope").With(target);

                    Assert.That(handle.IsAlive, Is.True, "Table 2-2 / Alive x With.ValidTarget");
                    Assert.That(LastLog(owner).Tags[0].Target, Is.SameAs(Box(target)));
                    Assert.That(LastLog(target).IsTagged, Is.True);
                }, NotPrinted),
                Scenario("Alive_DisposeDifferentThread", m =>
                {
                    var owner = m.NewOwner("scope");
                    var handle = BlackboxHandle.Of(owner).Scope("open", "Scope");
                    var createdThreadId = Environment.CurrentManagedThreadId;
                    var disposedThreadId = -1;
                    var warningCount = m.WarningLogs.Count;
                    Assert.That(handle.IsAlive, Is.True, "Table 2-2 / Alive x Dispose.DifferentThread: handle must be alive before cross-thread dispose.");
                    var thread = new Thread(() =>
                    {
                        disposedThreadId = Environment.CurrentManagedThreadId;
                        handle.Dispose();
                    });
                    thread.Start();
                    thread.Join();

                    Assert.That(disposedThreadId, Is.GreaterThan(0), "Table 2-2 / Alive x Dispose.DifferentThread: dispose thread delegate did not run.");
                    Assert.That(disposedThreadId, Is.Not.EqualTo(createdThreadId), "Table 2-2 / Alive x Dispose.DifferentThread: dispose ran on the creating thread.");
                    Assert.That(handle.IsDisposed, Is.True, "Table 2-2 / Alive x Dispose.DifferentThread: handle was not disposed.");
                    Assert.That(NewWarnings(m, warningCount).Any(log => log.Contains("different thread")), Is.True, "Table 2-2 / Alive x Dispose.DifferentThread: warning was not logged.");
                }, NotPrinted),
                Scenario("Printed_DisposeDoesNotClose", m =>
                {
                    var owner = m.NewOwner("scope");
                    var handle = BlackboxHandle.Of(owner).Scope("open", "Scope");
                    Infrastructure.TryMarkPrinted();
                    m.Printed = true;
                    handle.Dispose();

                    Assert.That(Logs(owner).Count(log => log.ScopeType == ScopeType.Close), Is.EqualTo(0), "Table 2-2 / Alive x Dispose.Printed");
                }, NotPrinted)
            };
        }

        private static IReadOnlyList<TableScenario> ExertHandleScenarios()
        {
            return new[]
            {
                Scenario("Default_Dispose", m =>
                {
                    var before = BlackboxRuntime.CurrentSequence;
                    default(ExertHandle).Dispose();

                    Assert.That(BlackboxRuntime.CurrentSequence, Is.EqualTo(before), "Table 2-3 / Default x Dispose");
                }),
                Scenario("Alive_DisposeMergeTargetExists", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "call", "Run");
                    Box(target).WriteScope("target scope", "Scope");
                    handle.Dispose();
                    var targetLog = Logs(target).Single();

                    Assert.That(handle.IsDisposed, Is.True, "Table 2-3 / Alive x Dispose.MergeTargetExists");
                    Assert.That(targetLog.ScopeType, Is.EqualTo(ScopeType.Open));
                    Assert.That(targetLog.InteractionId, Is.GreaterThanOrEqualTo(0));
                    Assert.That(targetLog.Message, Does.Contain("call"));
                }, NotPrinted),
                Scenario("Alive_DisposeMergeTargetMissing", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "call", "Run");
                    handle.Dispose();

                    Assert.That(Logs(target).Count, Is.EqualTo(1), "Table 2-3 / Alive x Dispose.MergeTargetMissing");
                    Assert.That(Logs(target)[0].ScopeType, Is.EqualTo(ScopeType.None));
                }, NotPrinted),
                Scenario("Disposed_DisposeAgain", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "call", "Run");
                    handle.Dispose();
                    var count = Logs(target).Count;
                    handle.Dispose();

                    Assert.That(Logs(target).Count, Is.EqualTo(count), "Table 2-3 / Disposed x Dispose");
                }, NotPrinted),
                Scenario("Alive_DisposeDifferentThread", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "call", "Run");
                    var createdThreadId = Environment.CurrentManagedThreadId;
                    var disposedThreadId = -1;
                    var warningCount = m.WarningLogs.Count;
                    Assert.That(handle.IsAlive, Is.True, "Table 2-3 / Alive x Dispose.DifferentThread: handle must be alive before cross-thread dispose.");
                    var thread = new Thread(() =>
                    {
                        disposedThreadId = Environment.CurrentManagedThreadId;
                        handle.Dispose();
                    });
                    thread.Start();
                    thread.Join();

                    Assert.That(disposedThreadId, Is.GreaterThan(0), "Table 2-3 / Alive x Dispose.DifferentThread: dispose thread delegate did not run.");
                    Assert.That(disposedThreadId, Is.Not.EqualTo(createdThreadId), "Table 2-3 / Alive x Dispose.DifferentThread: dispose ran on the creating thread.");
                    Assert.That(handle.IsDisposed, Is.True, "Table 2-3 / Alive x Dispose.DifferentThread: handle was not disposed.");
                    Assert.That(NewWarnings(m, warningCount).Any(log => log.Contains("different thread")), Is.True, "Table 2-3 / Alive x Dispose.DifferentThread: warning was not logged.");
                }, NotPrinted),
                Scenario("Printed_DisposeDoesNotMerge", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "call", "Run");
                    Box(target).WriteScope("target scope", "Scope");
                    Infrastructure.TryMarkPrinted();
                    m.Printed = true;
                    handle.Dispose();

                    Assert.That(Logs(target).Count, Is.EqualTo(2), "Table 2-3 / Alive x Dispose.Printed");
                }, NotPrinted)
            };
        }

        private static IReadOnlyList<TableScenario> BlackboxRuntimeScenarios()
        {
            return new[]
            {
                Scenario("Ready_CountersIncrease", m =>
                {
                    var id = BlackboxRuntime.GetNextBlackboxId();
                    var nextId = BlackboxRuntime.GetNextBlackboxId();
                    var interaction = BlackboxRuntime.GetNextInteractionId();
                    var nextInteraction = BlackboxRuntime.GetNextInteractionId();
                    var sequence = BlackboxRuntime.GetNextSequence();
                    var nextSequence = BlackboxRuntime.GetNextSequence();

                    Assert.That(nextId, Is.EqualTo(id + 1), "Table 3-1 / Ready x GetNextBlackboxId");
                    Assert.That(nextInteraction, Is.EqualTo(interaction + 1), "Table 3-1 / Ready x GetNextInteractionId");
                    Assert.That(nextSequence, Is.EqualTo(sequence + 1), "Table 3-1 / Ready x GetNextSequence");
                }),
                Scenario("Ready_Reset", m =>
                {
                    BlackboxRuntime.GetNextBlackboxId();
                    BlackboxRuntime.GetNextInteractionId();
                    BlackboxRuntime.GetNextSequence();
                    BlackboxRuntime.Reset();

                    Assert.That(BlackboxRuntime.GetNextBlackboxId(), Is.EqualTo(0), "Table 3-1 / Ready x Reset");
                    Assert.That(BlackboxRuntime.GetNextInteractionId(), Is.EqualTo(0));
                    Assert.That(BlackboxRuntime.GetNextSequence(), Is.EqualTo(0));
                })
            };
        }

        private static IReadOnlyList<TableScenario> BlackboxRegistryScenarios()
        {
            return new[]
            {
                Scenario("Empty_GetBlackboxNull", m =>
                {
                    Assert.Throws<ArgumentNullException>(() => BlackboxRegistry.GetBlackbox(null), "Table 3-2 / Empty x GetBlackbox.NullSubject");
                }),
                Scenario("Registered_GetBlackboxSameOwner", m =>
                {
                    var owner = m.NewOwner("registry");
                    var first = BlackboxRegistry.GetBlackbox(owner);
                    var second = BlackboxRegistry.GetBlackbox(owner);

                    Assert.That(second, Is.SameAs(first), "Table 3-2 / Registered x GetBlackbox.ValidSubject");
                    Assert.That(BlackboxRegistry.Contains(owner), Is.True);
                }),
                Scenario("Registered_ContainsCount", m =>
                {
                    var before = BlackboxRegistry.Count();
                    var owner = m.NewOwner("registry");
                    BlackboxRegistry.GetBlackbox(owner);

                    Assert.That(BlackboxRegistry.Contains(owner), Is.True, "Table 3-2 / Registered x Contains");
                    Assert.That(BlackboxRegistry.Count(), Is.EqualTo(before + 1), "Table 3-2 / Registered x Count");
                    Assert.Throws<ArgumentNullException>(() => BlackboxRegistry.Contains(null));
                }),
                Scenario("Any_ForceReset", m =>
                {
                    BlackboxRegistry.GetBlackbox(m.NewOwner("registry"));
                    BlackboxRegistry.ForceReset();
                    m.Printed = false;

                    Assert.That(BlackboxRegistry.Count(), Is.EqualTo(0), "Table 3-2 / Any x ForceReset");
                    Assert.That(BlackboxRuntime.GetNextBlackboxId(), Is.EqualTo(0));
                }),
                Scenario("StrongReferenceOn", m =>
                {
                    Infrastructure.StrongReference = true;
                    var owner = m.NewOwner("strong");
                    var box = BlackboxRegistry.GetBlackbox(owner);

                    Assert.That(box.Owner, Is.SameAs(owner), "Table 3-2 / Empty x GetBlackbox.StrongReferenceOn");
                }),
                Scenario("StrongReferenceOff", m =>
                {
                    Infrastructure.StrongReference = false;
                    var box = CreateWeakBlackbox(m, out var weakReference);
                    ForceFullCollection();

                    Assert.That(box.OwnerString, Does.Contain("weak"), "Table 3-2 / Empty x GetBlackbox.StrongReferenceOff");
                    if (!weakReference.IsAlive)
                        Assert.That(box.OwnerString, Does.Contain("Reference Lost"));
                })
            };
        }

        private static IReadOnlyList<TableScenario> InfrastructureScenarios()
        {
            return new[]
            {
                Scenario("Ready_ConfigureSet", m =>
                {
                    var directory = m.CreateTempDirectory();
                    Action<string> normal = text => m.NormalLogs.Add("N:" + text);
                    Action<string> warning = text => m.WarningLogs.Add("W:" + text);

                    BlackboxHandle.Configure(directory, normal, warning, true, ExportFormat.Txt, FullExportOption.Focused, OpenLogOption.Never, ExceptionHandlingOption.CrashExport, TargetTypes.Name);
                    BlackboxHandle.MaxLogCount = 3;
                    BlackboxHandle.DefaultRecursionDepth = 2;

                    Assert.That(Infrastructure.LogDirectory, Is.EqualTo(directory), "Table 3-3 / Ready x Configure/Set");
                    Assert.That(Infrastructure.NormalLogger, Is.SameAs(normal));
                    Assert.That(Infrastructure.WarningLogger, Is.SameAs(warning));
                    Assert.That(Infrastructure.StrongReference, Is.True);
                    Assert.That(Infrastructure.MaxLogCount, Is.EqualTo(3));
                    Assert.That(Infrastructure.DefaultRecursionDepth, Is.EqualTo(2));
                }),
                Scenario("Ready_Resolve", m =>
                {
                    Infrastructure.ExportFormat = ExportFormat.Txt;
                    Infrastructure.FullExportOption = FullExportOption.Focused;
                    Infrastructure.OpenLogOption = OpenLogOption.Never;
                    Infrastructure.ExceptionHandlingOption = ExceptionHandlingOption.CrashExport;
                    Infrastructure.TagTargetTypes = TargetTypes.Name;

                    Assert.That(ExportFormat.BasedOnSettings.Resolve(), Is.EqualTo(ExportFormat.Txt), "Table 3-3 / Ready x Resolve");
                    Assert.That(ExportFormat.Html.Resolve(), Is.EqualTo(ExportFormat.Html));
                    Assert.That(FullExportOption.BasedOnSettings.Resolve(), Is.EqualTo(FullExportOption.Focused));
                    Assert.That(OpenLogOption.BasedOnSettings.Resolve(), Is.EqualTo(OpenLogOption.Never));
                    Assert.That(ExceptionHandlingOption.BasedOnSettings.Resolve(), Is.EqualTo(ExceptionHandlingOption.CrashExport));
                    Assert.That(TargetTypes.BasedOnSettings.Resolve(), Is.EqualTo(TargetTypes.Name));
                }),
                Scenario("Ready_Log", m =>
                {
                    Infrastructure.Log("normal");
                    Infrastructure.Log("warning", LogLevel.Warning);

                    Assert.That(m.NormalLogs.Last(), Does.EndWith("normal"), "Table 3-3 / Ready x Log");
                    Assert.That(m.WarningLogs.Last(), Does.EndWith("warning"));
                }),
                Scenario("Ready_Printed_Reset", m =>
                {
                    Assert.That(Infrastructure.TryMarkPrinted(), Is.True, "Table 3-3 / Ready x TryMarkPrinted");
                    Assert.That(Infrastructure.TryMarkPrinted(), Is.False, "Table 3-3 / Printed x TryMarkPrinted");
                    Infrastructure.ForceResetRuntimeState();
                    Assert.That(Infrastructure.TryMarkPrinted(), Is.True, "Table 3-3 / Printed x ForceResetRuntimeState");
                    m.Printed = true;
                }, NotPrinted)
            };
        }

        private static IReadOnlyList<TableScenario> LogContextScenarios()
        {
            return new[]
            {
                Scenario("Ready_EnqueueLog", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.EnqueueLog("hello", 1, "Run");

                    Assert.That(context.GetLogs().Single().Message, Is.EqualTo("hello"), "Table 4-1 / Ready x EnqueueLog");
                }),
                Scenario("RingBufferFull_RecentLogs", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 2);
                    context.EnqueueLog("old", 1, "Run");
                    context.EnqueueLog("middle", 2, "Run");
                    context.EnqueueLog("new", 3, "Run");

                    Assert.That(context.GetLogs().Select(log => log.Message), Is.EqualTo(new[] { "middle", "new" }), "Table 4-1 / RingBufferFull x EnqueueLog/GetLogs");
                }),
                Scenario("Ready_OpenCloseScope", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.OpenScope("open", 1, "Scope", 1);
                    context.CloseScope(1, 2);
                    var logs = context.GetLogs().ToList();

                    Assert.That(logs[0].ScopeType, Is.EqualTo(ScopeType.Open), "Table 4-1 / Ready x OpenScope");
                    Assert.That(logs[1].ScopeType, Is.EqualTo(ScopeType.Close), "Table 4-1 / ScopeOpen x CloseScope.CloseNewest");
                }),
                Scenario("Ready_CloseMissingWarns", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.CloseScope(99, 1);

                    Assert.That(m.WarningLogs.Last(), Does.Contain("not open"), "Table 4-1 / Ready x CloseScope");
                }),
                Scenario("ScopeOpen_CloseOuterFirst", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.OpenScope("outer", 1, "Outer", 1);
                    context.OpenScope("inner", 2, "Inner", 2);
                    context.CloseScope(1, 3);
                    var logs = context.GetLogs().ToList();

                    Assert.That(logs.Count(log => log.ScopeType == ScopeType.Close), Is.EqualTo(2), "Table 4-1 / ScopeOpen x CloseOuterFirst");
                    Assert.That(m.WarningLogs.Last(), Does.Contain("automatically"));
                }),
                Scenario("ScopeOpen_CloseFromDifferentThread", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.OpenScope("outer", 1, "Outer", 1);
                    context.OpenScope("inner", 2, "Inner", 2);
                    var thread = new Thread(() => context.CloseScope(1, 3));
                    thread.Start();
                    thread.Join();

                    Assert.That(m.WarningLogs.Last(), Does.Contain("different thread"), "Table 4-1 / ScopeOpen x CloseFromDifferentThread");
                }),
                Scenario("ScopeOpen_ResolveWith", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var target = m.NewBlackbox("target");
                    var context = new LogContext(owner, 10);
                    context.EnqueueLog("uses %0", 1, "Run");
                    context.ResolveWith(0, new[] { target }, TargetTypes.Full);

                    Assert.That(context.GetLogs().Single().Tags[0].Target, Is.SameAs(target), "Table 4-1 / ScopeOpen x ResolveWith.ValidTarget");
                    Assert.That(LastLog(target).IsTagged, Is.True);
                }),
                Scenario("ScopeOpen_TryMergeScopeAdjacent", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var source = m.NewBlackbox("source");
                    var context = new LogContext(owner, 10);
                    context.EnqueueLog("call", 1, "Run", -1, 4, null, source);
                    context.OpenScope("scope", 2, "Scope", 1);

                    Assert.That(context.TryMergeScope(4), Is.True, "Table 4-1 / ScopeOpen x TryMergeScope.MergeAdjacent");
                    Assert.That(context.GetLogs().Single().InteractionId, Is.EqualTo(4));
                }),
                Scenario("ScopeOpen_TryMergeScopeRejects", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var source = m.NewBlackbox("source");
                    var context = new LogContext(owner, 10);
                    context.EnqueueLog("call", 1, "Run", -1, 5, null, source);
                    context.EnqueueLog("gap", 2, "Run");
                    context.OpenScope("scope", 3, "Scope", 1);

                    Assert.That(context.TryMergeScope(5), Is.False, "Table 4-1 / ScopeOpen x TryMergeScope.MergeNonAdjacent");
                }),
                Scenario("Ready_GetLogsMaxSequence", m =>
                {
                    var owner = m.NewBlackbox("context");
                    var context = new LogContext(owner, 10);
                    context.EnqueueLog("one", 1, "Run");
                    context.EnqueueLog("two", 2, "Run");

                    Assert.That(context.GetLogs(1).Select(log => log.Message), Is.EqualTo(new[] { "one" }), "Table 4-1 / Ready x GetLogs");
                })
            };
        }

        private static IReadOnlyList<TableScenario> BlackboxScenarios()
        {
            return new[]
            {
                Scenario("Ready_Write", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    Box(owner).Write("hello", "Run");

                    Assert.That(LastLog(owner).Message, Is.EqualTo("hello"), "Table 4-2 / Ready x Write");
                }, NotPrinted),
                Scenario("Ready_Scope", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    var scope = Box(owner).WriteScope("scope", "Scope");

                    Assert.That(scope.IsAlive, Is.True, "Table 4-2 / Ready x Scope");
                    Assert.That(LastLog(owner).ScopeType, Is.EqualTo(ScopeType.Open));
                }, NotPrinted),
                Scenario("Ready_ExertMessageOtherNull", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    Assert.Throws<ArgumentNullException>(() => Box(owner).ExertMessage(null, "call", "Run"), "Table 4-2 / Ready x ExertMessage.OtherNull");
                }, NotPrinted),
                Scenario("Ready_ExertMessageOtherSelf", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    var box = Box(owner);
                    box.ExertMessage(box, "self", "Run");
                    var log = LastLog(owner);

                    Assert.That(log.ExertedBy, Is.SameAs(box), "Table 4-2 / Ready x ExertMessage.OtherSelf");
                    Assert.That(log.ExertingTo, Is.SameAs(box));
                }, NotPrinted),
                Scenario("Ready_ExertMessageOtherPeer", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    Box(source).ExertMessage(Box(target), "peer", "Run");

                    Assert.That(LastLog(source).ExertingTo, Is.SameAs(Box(target)), "Table 4-2 / Ready x ExertMessage.OtherPeer");
                    Assert.That(LastLog(target).ExertedBy, Is.SameAs(Box(source)));
                }, NotPrinted),
                Scenario("Ready_ExertOtherPeer", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = Box(source).Exert(Box(target), "peer", "Run");

                    Assert.That(handle.IsAlive, Is.True, "Table 4-2 / Ready x Exert.OtherPeer");
                }, NotPrinted),
                Scenario("Printed_NoNewLogs", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    Box(owner).Write("before", "Run");
                    Infrastructure.TryMarkPrinted();
                    m.Printed = true;
                    Box(owner).Write("after", "Run");

                    Assert.That(Logs(owner).Select(log => log.Message), Is.EqualTo(new[] { "before" }), "Table 4-2 / Printed x Write");
                }, NotPrinted),
                Scenario("Ready_GetLogsSortsMultiContext", m =>
                {
                    var owner = m.NewOwner("blackbox");
                    var box = Box(owner);
                    box.Write("main", "Run");
                    var thread = new Thread(() => box.Write("worker", "Run"));
                    thread.Start();
                    thread.Join();

                    Assert.That(box.GetLogs().Select(log => log.Sequence), Is.Ordered, "Table 4-2 / Ready x GetLogs.MultiContext");
                    Assert.That(box.GetLogsByContext().Length, Is.GreaterThanOrEqualTo(2), "Table 4-2 / Ready x GetLogsByContext");
                }, NotPrinted),
                Scenario("OwnerReferenceLost_Fallback", m =>
                {
                    Infrastructure.StrongReference = false;
                    var box = CreateWeakBlackbox(m, out var weakReference);
                    ForceFullCollection();

                    Assert.That(box.OwnerString, Does.Contain("weak"), "Table 4-2 / OwnerReferenceLost");
                    if (!weakReference.IsAlive)
                        Assert.That(box.OwnerString, Does.Contain("Reference Lost"));
                })
            };
        }

        private static IReadOnlyList<TableScenario> BlackboxHandleScenarios()
        {
            return new[]
            {
                Scenario("Valid_Of_NullSubject", m =>
                {
                    var owner = m.NewOwner("handle");

                    Assert.That(BlackboxHandle.Of(owner).IsValid, Is.True, "Table 5-1 / Valid x Of.ValidSubject");
                    Assert.Throws<ArgumentNullException>(() => BlackboxHandle.Of<object>(null), "Table 5-1 / Valid x Of.NullSubject");
                }),
                Scenario("DefaultInvalid_ReturnsFallbacks", m =>
                {
                    var target = m.NewOwner("target");
                    var handle = default(BlackboxHandle);

                    Assert.That(handle.Write("message", "Run").With(target), Is.EqualTo("message"), "Table 5-1 / Default/Invalid x Write");
                    Assert.That(handle.ExertMessage(target, "message", "Run"), Is.EqualTo("message"));
                    Assert.That(handle.Scope("scope", "Run").IsDisposed, Is.True);
                }),
                Scenario("Valid_Construct", m =>
                {
                    var owner = m.NewOwner("handle");
                    var scope = BlackboxHandle.Of(owner).Construct("created", out var cached, "Ctor");

                    Assert.That(cached.IsValid, Is.True, "Table 5-1 / Valid x Construct");
                    Assert.That(scope.IsAlive, Is.True);
                    Assert.That(LastLog(owner).Message, Does.Contain("[Ctor:"));
                }, NotPrinted),
                Scenario("Valid_When", m =>
                {
                    var owner = m.NewOwner("handle");
                    var handle = BlackboxHandle.Of(owner);

                    Assert.That(handle.When(true).IsValid, Is.True, "Table 5-1 / Valid x When.True");
                    Assert.That(handle.When(false).IsValid, Is.False, "Table 5-1 / Valid x When.False");
                    Assert.Throws<ArgumentNullException>(() => handle.When((Func<bool>)null));
                }),
                Scenario("Valid_Dispose_Write_Scope", m =>
                {
                    var owner = m.NewOwner("handle");
                    var handle = BlackboxHandle.Of(owner);
                    handle.Dispose("done", "Dispose");
                    handle.Write("self", "Write");
                    var scope = handle.Scope("scope", "Scope");

                    Assert.That(scope.IsAlive, Is.True, "Table 5-1 / Valid x Scope");
                    Assert.That(Logs(owner).Any(log => log.Message.Contains("[Disposed:")), Is.True, "Table 5-1 / Valid x Dispose");
                    Assert.That(Logs(owner).Any(log => log.Message == "self"), Is.True, "Table 5-1 / Valid x Write");
                }, NotPrinted),
                Scenario("Valid_ExertMessage_Exert", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var handle = BlackboxHandle.Of(source);

                    Assert.Throws<ArgumentNullException>(() => handle.ExertMessage<object>(null, "bad", "Run"), "Table 5-1 / Valid x ExertMessage.OtherNull");
                    Assert.That(handle.ExertMessage(target, "peer", "Run"), Is.EqualTo("peer"), "Table 5-1 / Valid x ExertMessage.OtherPeer");
                    Assert.Throws<ArgumentNullException>(() => handle.Exert<object>(null, "bad", "Run"), "Table 5-1 / Valid x Exert.OtherNull");
                    Assert.That(handle.Exert(target, "peer", "Run").IsAlive, Is.True, "Table 5-1 / Valid x Exert.OtherPeer");
                }, NotPrinted),
                Scenario("Valid_WriteErrorTargets", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var message = BlackboxHandle.Of(source).WriteError("boom", new BlackboxHandle.ErrorContainer(("target", target)), ExceptionHandlingOption.None, "Run");
                    var log = LastLog(source);

                    Assert.That(message, Is.EqualTo("boom"), "Table 5-1 / Valid x WriteError.ErrorTargetsPresent");
                    Assert.That(log.Message, Does.Contain("[Error] boom"));
                    Assert.That(log.Tags[0].Target, Is.SameAs(Box(target)));
                }, NotPrinted),
                Scenario("Valid_WriteErrorCrashExport", m =>
                {
                    var owner = m.NewOwner("handle");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    BlackboxHandle.ExportFormat = ExportFormat.Html;
                    BlackboxHandle.OpenLogOption = OpenLogOption.Never;
                    BlackboxHandle.Of(owner).WriteError("boom", exceptionHandlingOption: ExceptionHandlingOption.CrashExport, methodName: "Run");
                    m.Printed = true;

                    Assert.That(GetFiles(directory, "[CRASH]*.html").Length, Is.EqualTo(1), "Table 5-1 / Valid x WriteError.ExceptionHandlingCrashExport");
                    Assert.That(Logs(owner).Any(log => log.Message.Contains("[STACK TRACE]")), Is.True, "Table 5-1 / Valid x CrashExport");
                }, NotPrinted),
                Scenario("Valid_Export_Duplicate_Invalid", m =>
                {
                    var owner = m.NewOwner("handle");
                    var directory = m.CreateTempDirectory();
                    var handle = BlackboxHandle.Of(owner);
                    handle.Write("hello", "Run");
                    BlackboxHandle.LogDirectory = directory;
                    BlackboxHandle.ExportFormat = ExportFormat.Txt;
                    BlackboxHandle.OpenLogOption = OpenLogOption.Never;

                    default(BlackboxHandle).Export();
                    handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                    handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                    m.Printed = true;

                    Assert.That(m.WarningLogs.Any(log => log.Contains("invalid")), Is.True, "Table 5-1 / Default/Invalid x Export");
                    Assert.That(GetFiles(directory, "*.txt").Length, Is.EqualTo(1), "Table 5-1 / Valid x Export.FirstExport");
                    Assert.That(m.WarningLogs.Any(log => log.Contains("already been exported")), Is.True, "Table 5-1 / Printed x Export.DuplicateExport");
                }, NotPrinted),
                Scenario("Printed_MessageFallback", m =>
                {
                    var owner = m.NewOwner("handle");
                    var handle = BlackboxHandle.Of(owner);
                    Infrastructure.TryMarkPrinted();
                    m.Printed = true;

                    Assert.That(handle.Write("message", "Run").With(m.NewOwner("target")), Is.EqualTo("message"), "Table 5-1 / Printed x Write");
                    Assert.That(handle.ExertMessage(m.NewOwner("target"), "message", "Run"), Is.EqualTo("message"), "Table 5-1 / Printed x ExertMessage");
                }, NotPrinted)
            };
        }

        private static IReadOnlyList<TableScenario> ExportToolsScenarios()
        {
            return new[]
            {
                Scenario("BuildExportGraph_RootOnly", m =>
                {
                    var owner = m.NewOwner("export");
                    Box(owner).Write("root", "Run");
                    var nodes = BlackThunder.BlackboxSystem.Exporters.Tools.BuildExportGraph(Box(owner), 0, false);

                    Assert.That(nodes.Count, Is.EqualTo(1), "Table 6-1 / RootOnly x BuildExportGraph");
                    Assert.That(nodes[0].Blackbox, Is.SameAs(Box(owner)));
                }, NotPrinted),
                Scenario("BuildExportGraph_FocusedIncoming", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    Box(source).ExertMessage(Box(target), "call", "Run");
                    var nodes = BlackThunder.BlackboxSystem.Exporters.Tools.BuildExportGraph(Box(target), 1, false);

                    Assert.That(nodes.Any(node => node.Blackbox == Box(source)), Is.True, "Table 6-1 / Focused x BuildExportGraph");
                }, NotPrinted),
                Scenario("BuildExportGraph_FullOutgoing", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    Box(source).ExertMessage(Box(target), "call", "Run");
                    var nodes = BlackThunder.BlackboxSystem.Exporters.Tools.BuildExportGraph(Box(source), 1, true);

                    Assert.That(nodes.Any(node => node.Blackbox == Box(target)), Is.True, "Table 6-1 / Full x BuildExportGraph");
                }, NotPrinted),
                Scenario("BuildExportGraph_DepthLimited", m =>
                {
                    var first = m.NewOwner("first");
                    var second = m.NewOwner("second");
                    var third = m.NewOwner("third");
                    Box(first).ExertMessage(Box(second), "call", "Run");
                    Box(second).ExertMessage(Box(third), "call", "Run");
                    var nodes = BlackThunder.BlackboxSystem.Exporters.Tools.BuildExportGraph(Box(first), 1, true);

                    Assert.That(nodes.Any(node => node.Blackbox == Box(second)), Is.True, "Table 6-1 / DepthLimited x BuildExportGraph");
                    Assert.That(nodes.Any(node => node.Blackbox == Box(third)), Is.False);
                }, NotPrinted),
                Scenario("FlattenSteps_EmptyScopePair", m =>
                {
                    var owner = m.NewBlackbox("export");
                    var logs = new[]
                    {
                        new LogData(owner, "open", DateTime.UtcNow, 1, "Scope", ScopeType.Open, 1, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "close", DateTime.UtcNow, 2, "Scope", ScopeType.Close, 1, 1, "main", -1, null, null, null, null)
                    };
                    var flattened = BlackThunder.BlackboxSystem.Exporters.Tools.FlattenSteps(logs);

                    Assert.That(flattened.Count, Is.EqualTo(1), "Table 6-1 / RootOnly x FlattenSteps");
                    Assert.That(flattened[0].ScopeType, Is.EqualTo(ScopeType.Step));
                    Assert.That(flattened[0].Message, Is.EqualTo("open > close"));
                }),
                Scenario("FlattenSteps_PreservesNonEmptyScope", m =>
                {
                    var owner = m.NewBlackbox("export");
                    var logs = new[]
                    {
                        new LogData(owner, "open", DateTime.UtcNow, 1, "Scope", ScopeType.Open, 1, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "inside", DateTime.UtcNow, 2, "Run", ScopeType.None, -1, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "close", DateTime.UtcNow, 3, "Scope", ScopeType.Close, 1, 1, "main", -1, null, null, null, null)
                    };

                    Assert.That(BlackThunder.BlackboxSystem.Exporters.Tools.FlattenSteps(logs).Count, Is.EqualTo(3), "Table 6-1 / Focused x FlattenSteps");
                }),
                Scenario("ResolveScopeDepths", m =>
                {
                    var owner = m.NewBlackbox("export");
                    var logs = new[]
                    {
                        new LogData(owner, "outer", DateTime.UtcNow, 1, "Outer", ScopeType.Open, 1, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "inner", DateTime.UtcNow, 2, "Inner", ScopeType.Open, 2, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "", DateTime.UtcNow, 3, "Inner", ScopeType.Close, 2, 1, "main", -1, null, null, null, null),
                        new LogData(owner, "", DateTime.UtcNow, 4, "Outer", ScopeType.Close, 1, 1, "main", -1, null, null, null, null)
                    };
                    var resolved = BlackThunder.BlackboxSystem.Exporters.Tools.ResolveScopeDepths(logs);

                    Assert.That(resolved.Select(log => log.ScopeDepth), Is.EqualTo(new[] { 0, 1, 1, 0 }), "Table 6-1 / RootOnly x ResolveScopeDepths");
                }),
                Scenario("TrimSmart", m =>
                {
                    var invalidChar = Path.GetInvalidFileNameChars().First();
                    var longName = new string('a', 40);

                    Assert.That(BlackThunder.BlackboxSystem.Exporters.Tools.TrimSmart(null), Is.EqualTo("null"), "Table 6-1 / FileName x TrimSmart");
                    Assert.That(BlackThunder.BlackboxSystem.Exporters.Tools.TrimSmart(new string(invalidChar, 3)), Is.EqualTo("null"));
                    Assert.That(BlackThunder.BlackboxSystem.Exporters.Tools.TrimSmart("a" + invalidChar + "b"), Is.EqualTo("ab"));
                    Assert.That(BlackThunder.BlackboxSystem.Exporters.Tools.TrimSmart(longName).Length, Is.LessThan(longName.Length));
                })
            };
        }

        private static IReadOnlyList<TableScenario> ExporterScenarios()
        {
            return new[]
            {
                Scenario("TxtExporter_LogDirectoryMissing", m =>
                {
                    var owner = m.NewOwner("exporter");
                    Box(owner).Write("hello", "Run");
                    BlackboxHandle.LogDirectory = null;

                    Assert.Throws<InvalidOperationException>(() => TxtExporter.Export(Box(owner), 1, false, false, false), "Table 6-2 / LogDirectoryMissing x Export.Normal");
                }, NotPrinted),
                Scenario("TxtExporter_NormalCrashFiles", m =>
                {
                    var owner = m.NewOwner("exporter");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    Box(owner).Write("hello", "Run");
                    TxtExporter.Export(Box(owner), 1, false, false, false);
                    TxtExporter.Export(Box(owner), 1, true, false, false);

                    Assert.That(GetFiles(directory, "Blackbox*.txt").Length, Is.EqualTo(1), "Table 6-2 / TxtReady x Export.Normal");
                    Assert.That(GetFiles(directory, "[CRASH]*.txt").Length, Is.EqualTo(1), "Table 6-2 / TxtReady x Export.Crash");
                }, NotPrinted),
                Scenario("HtmlExporter_LogDirectoryMissing", m =>
                {
                    var owner = m.NewOwner("exporter");
                    Box(owner).Write("hello", "Run");
                    BlackboxHandle.LogDirectory = null;

                    Assert.Throws<InvalidOperationException>(() => HtmlExporter.Export(Box(owner), 1, false, false, false), "Table 6-2 / LogDirectoryMissing x Export.Crash");
                }, NotPrinted),
                Scenario("HtmlExporter_NormalCrashFiles", m =>
                {
                    var owner = m.NewOwner("exporter");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    Box(owner).Write("hello", "Run");
                    HtmlExporter.Export(Box(owner), 1, false, false, false);
                    HtmlExporter.Export(Box(owner), 1, true, false, false);

                    Assert.That(GetFiles(directory, "Blackbox*.html").Length, Is.EqualTo(1), "Table 6-2 / HtmlReady x Export.Normal");
                    Assert.That(GetFiles(directory, "[CRASH]*.html").Length, Is.EqualTo(1), "Table 6-2 / HtmlReady x Export.Crash");
                }, NotPrinted),
                Scenario("HtmlExporter_InteractionLinks", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    Box(source).ExertMessage(Box(target), "call", "Run");
                    HtmlExporter.Export(Box(source), 1, false, true, false);
                    var html = File.ReadAllText(GetFiles(directory, "Blackbox*.html").Single());

                    Assert.That(html, Does.Contain("href='#log_"), "Table 6-2 / HtmlReady x Export.Normal");
                    Assert.That(html, Does.Contain("interaction"));
                }, NotPrinted),
                Scenario("Exporter_OpenLogFailureWarning", m =>
                {
                    var owner = m.NewOwner("exporter");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    Box(owner).Write("hello", "Run");
                    var original = TxtExporter.OpenLogProcess;
                    TxtExporter.OpenLogProcess = _ => throw new InvalidOperationException("open failed");

                    try
                    {
                        TxtExporter.Export(Box(owner), 1, false, false, true);
                    }
                    finally
                    {
                        TxtExporter.OpenLogProcess = original;
                    }

                    Assert.That(m.WarningLogs.Last(), Does.Contain("Failed to open"), "Table 6-2 / TxtReady x OpenLog");
                }, NotPrinted)
            };
        }

        private static IReadOnlyList<TableScenario> IntegrationScenarios()
        {
            return new[]
            {
                Scenario("SingleOwnerHistory", m =>
                {
                    var owner = m.NewOwner("single");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    BlackboxHandle.ExportFormat = ExportFormat.Txt;
                    BlackboxHandle.OpenLogOption = OpenLogOption.Never;
                    var construct = BlackboxHandle.Of(owner).Construct("init", out var handle, "Ctor");
                    handle.Write("inside", "Run");
                    construct.Dispose();
                    handle.Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                    m.Printed = true;
                    var text = File.ReadAllText(GetFiles(directory, "*.txt").Single());

                    Assert.That(text, Does.Contain("inside"), "Table 7 / SingleOwnerHistory x Run");
                    Assert.That(text, Does.Contain("single"));
                }, NotPrinted),
                Scenario("TwoOwnerInteraction", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    var exert = BlackboxHandle.Of(source).Exert(target, "call", "Run");
                    var scope = BlackboxHandle.Of(target).Scope("target scope", "Scope");
                    exert.Dispose();
                    scope.Dispose();
                    BlackboxHandle.Of(source).Export(1, ExportFormat.Html, FullExportOption.Full, OpenLogOption.Never);
                    m.Printed = true;
                    var html = File.ReadAllText(GetFiles(directory, "*.html").Single());

                    Assert.That(html, Does.Contain("source"), "Table 7 / TwoOwnerInteraction x Run");
                    Assert.That(html, Does.Contain("target"));
                    Assert.That(html, Does.Contain("interaction"));
                }, NotPrinted),
                Scenario("TagTargetFlow", m =>
                {
                    var source = m.NewOwner("source");
                    var target = m.NewOwner("target");
                    BlackboxHandle.Of(source).Write("tag %0", "Run").With(target, TargetTypes.Full);
                    BlackboxHandle.Of(source).Scope("scope %0", "Scope").With(target).Dispose();

                    Assert.That(Logs(source).Any(log => log.Tags != null && log.Tags.Any(tag => tag.Target == Box(target))), Is.True, "Table 7 / TagTargetFlow x Run");
                    Assert.That(Logs(target).Any(log => log.IsTagged), Is.True);
                }, NotPrinted),
                Scenario("ErrorFlow", m =>
                {
                    var owner = m.NewOwner("error");
                    var directory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = directory;
                    BlackboxHandle.Of(owner).WriteError("boom", exceptionHandlingOption: ExceptionHandlingOption.CrashExport, methodName: "Run");
                    m.Printed = true;

                    Assert.That(Logs(owner).Any(log => log.Message.Contains("[Error] boom")), Is.True, "Table 7 / ErrorFlow x Run");
                    Assert.That(GetFiles(directory, "[CRASH]*.html").Length, Is.EqualTo(1));
                }, NotPrinted),
                Scenario("ResetAndRerun", m =>
                {
                    var first = m.NewOwner("first");
                    var firstDirectory = m.CreateTempDirectory();
                    BlackboxHandle.LogDirectory = firstDirectory;
                    BlackboxHandle.Of(first).Write("before", "Run");
                    BlackboxHandle.Of(first).Export(format: ExportFormat.Txt, openLogOption: OpenLogOption.Never);
                    BlackboxRegistry.ForceReset();
                    m.Printed = false;
                    var second = m.NewOwner("second");
                    BlackboxHandle.Of(second).Write("after", "Run");

                    Assert.That(BlackboxRegistry.Count(), Is.EqualTo(1), "Table 7 / ResetAndRerun x Run");
                    Assert.That(LastLog(second).Message, Is.EqualTo("after"));
                }, NotPrinted),
                Scenario("RingBufferLimit", m =>
                {
                    BlackboxHandle.MaxLogCount = 2;
                    var owner = m.NewOwner("ring");
                    var handle = BlackboxHandle.Of(owner);
                    handle.Write("one", "Run");
                    handle.Write("two", "Run");
                    handle.Write("three", "Run");

                    Assert.That(Logs(owner).Select(log => log.Message), Is.EqualTo(new[] { "two", "three" }), "Table 7 / RingBufferLimit x Run");
                }, NotPrinted)
            };
        }

        private static LogData NewFormatterLog(Blackbox owner, ScopeType scopeType)
        {
            return new LogData(owner, "message", DateTime.UtcNow, 1, "Run", scopeType, 1, 1, "main", -1, null, null, null, null);
        }

        private static TableScenario Scenario(string id, Action<BlackboxTableModel> execute)
        {
            return new TableScenario(id, execute, _ => true);
        }

        private static TableScenario Scenario(string id, Action<BlackboxTableModel> execute, Func<BlackboxTableModel, bool> canRun)
        {
            return new TableScenario(id, execute, canRun);
        }

        private static bool NotPrinted(BlackboxTableModel model)
        {
            return !model.Printed;
        }

        private static IEnumerable<string> NewWarnings(BlackboxTableModel model, int warningCount)
        {
            return model.WarningLogs.Skip(warningCount);
        }

        private static Blackbox Box(object owner)
        {
            return BlackboxRegistry.GetBlackbox(owner);
        }

        private static Blackbox Box(Blackbox box)
        {
            return box;
        }

        private static IReadOnlyList<LogData> Logs(object owner)
        {
            return Box(owner).GetLogs();
        }

        private static IReadOnlyList<LogData> Logs(Blackbox box)
        {
            return box.GetLogs();
        }

        private static LogData LastLog(object owner)
        {
            return Logs(owner).Last();
        }

        private static LogData LastLog(Blackbox box)
        {
            return Logs(box).Last();
        }

        private static string[] GetFiles(string directory, string pattern)
        {
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern)
                : Array.Empty<string>();
        }

        private static Blackbox CreateWeakBlackbox(BlackboxTableModel model, out WeakReference weakReference)
        {
            var owner = model.NewOwner("weak");
            weakReference = new WeakReference(owner);
            return BlackboxRegistry.GetBlackbox(owner);
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

    }
}
