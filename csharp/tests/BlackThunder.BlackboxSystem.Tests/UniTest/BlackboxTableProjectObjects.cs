using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackThunder.UniTest;

namespace BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxTableProject : Project<BlackboxTableModel>
    {
        // Front
        public string Name { get; }
        public IReadOnlyList<string> ScenarioIds => _scenarios.Select(scenario => scenario.Id).ToArray();
        public int CompleteExecutionDepth => _scenarios.Count + 1;

        // Internal
        private readonly IReadOnlyList<TableScenario> _scenarios;
        private readonly bool _continuous;
        private readonly int _continuousSeed;


        // Content
        public BlackboxTableProject(string name, bool continuous, int continuousSeed, IReadOnlyList<TableScenario> scenarios)
            : base(ExecutionMode.Inline)
        {
            if (scenarios == null)
                throw new ArgumentNullException(nameof(scenarios));
            if (scenarios.Count == 0 || scenarios.Any(scenario => scenario == null || string.IsNullOrWhiteSpace(scenario.Id)))
                throw new ArgumentException("Table projects require named scenarios.", nameof(scenarios));
            if (scenarios.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count() != scenarios.Count)
                throw new ArgumentException("Table scenario IDs must be unique within a project.", nameof(scenarios));

            Name = name;
            _continuous = continuous;
            _continuousSeed = continuousSeed;
            _scenarios = scenarios;
        }

        public override IEnumerable<ILab<BlackboxTableModel>> CreateLabs(BlackboxTableModel model)
        {
            var labs = new List<ILab<BlackboxTableModel>>();

            if (model.Subject == null)
            {
                labs.Add(new Lab<BlackboxTableModel>("Ignite_" + Name, actor: (m, _) => m.Ignite(Name)));
                return labs;
            }

            if (_continuous)
            {
                var available = _scenarios.Where(scenario => scenario.CanRun(model)).ToList();
                if (available.Count == 0)
                    return labs;

                // ExecuteContinuously normally receives every candidate lab, but it prepares all returned labs before selecting the next node.
                // Blackbox runtime/logger state is global, so this test keeps one deterministic random candidate per step instead.
                var randomIndex = model.GetDeterministicRandom(available.Count, _continuousSeed);
                labs.Add(CreateLab(available[randomIndex]));
                return labs;
            }

            // Complete coverage follows the declaration list, never a filtered or depth-limited prefix.
            // Every scenario arranges the state it exercises from a fresh recording lifetime.
            if (model.ExecutedScenarioIds.Count < _scenarios.Count)
                labs.Add(CreateLab(_scenarios[model.ExecutedScenarioIds.Count]));
            return labs;
        }

        private Lab<BlackboxTableModel> CreateLab(TableScenario scenario)
        {
            return new Lab<BlackboxTableModel>(
                Name + "_" + scenario.Id,
                actor: (model, _) =>
                {
                    if (!_continuous)
                        model.ResetRecordingState();
                    if (!scenario.CanRun(model))
                        throw new InvalidOperationException("The registered table scenario cannot run: " + Name + "/" + scenario.Id);

                    scenario.Execute(model);
                    model.ExecutedScenarioIds.Add(scenario.Id);
                });
        }
    }

    internal sealed class BlackboxTableModel : Model
    {
        // Front
        public readonly List<string> NormalLogs = new List<string>();
        public readonly List<string> WarningLogs = new List<string>();
        public readonly List<string> TempDirectories = new List<string>();
        public readonly List<string> ExecutedScenarioIds = new List<string>();

        private int _ownerIndex;

        public bool Printed;
        public string ProjectName;

        public void Ignite(string projectName)
        {
            ProjectName = projectName;
            Subject = projectName;
            ResetRecordingState();
        }

        public void ResetRecordingState()
        {
            if (BlackboxHandle.ActiveSessionCount != 0)
                throw new InvalidOperationException("A table scenario leaked an explicit session.");

            BlackboxHandle.RestartDefaultSession();
            Printed = false;
            _ownerIndex = 0;

            NormalLogs.Clear();
            WarningLogs.Clear();

            BlackboxHandle.Configure(
                null,
                NormalLogs.Add,
                WarningLogs.Add,
                false,
                ExportFormat.Html,
                FullExportOption.Full,
                OpenLogOption.Never,
                ExceptionHandlingOption.None,
                TargetTypes.Full,
                UseBlackboxOption.Use);

            BlackboxHandle.MaxLogCount = 100;
            BlackboxHandle.DefaultRecursionDepth = 100;
        }

        public NamedOwner NewOwner(string role)
        {
            _ownerIndex++;
            return new NamedOwner(ProjectName + "_" + role + "_" + _ownerIndex);
        }

        public Blackbox NewBlackbox(string role) => BlackboxTestDoubles.BlackboxFor(NewOwner(role));

        public string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "BlackboxUniTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            TempDirectories.Add(directory);
            return directory;
        }
    }

    internal sealed class TableScenario
    {
        public readonly string Id;
        public readonly Action<BlackboxTableModel> Execute;
        public readonly Func<BlackboxTableModel, bool> CanRun;

        public TableScenario(string id, Action<BlackboxTableModel> execute, Func<BlackboxTableModel, bool> canRun)
        {
            Id = id;
            Execute = execute;
            CanRun = canRun;
        }
    }

    internal sealed class NamedOwner
    {
        private readonly string _name;
        public string Name => _name;

        public NamedOwner(string name)
        {
            _name = name;
        }

        public override string ToString() => _name;
    }
}
