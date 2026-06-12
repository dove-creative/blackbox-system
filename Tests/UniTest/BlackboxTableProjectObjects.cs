#if BLACKBOX
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniTest;

namespace com.BlackThunder.BlackboxSystem.Tests
{
    internal sealed class BlackboxTableProject : Project<BlackboxTableModel>
    {
        private readonly IReadOnlyList<TableScenario> _scenarios;
        private readonly bool _continuous;
        private readonly int _continuousSeed;

        public string Name { get; private set; }

        public BlackboxTableProject(string name, bool continuous, int continuousSeed, IReadOnlyList<TableScenario> scenarios)
        {
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

            var available = _scenarios.Where(scenario => scenario.CanRun(model)).ToList();
            if (available.Count == 0)
                return labs;

            if (_continuous)
            {
                // ExecuteContinuously normally receives every candidate lab, but it prepares all returned labs before selecting the next node.
                // Blackbox runtime/logger state is global, so this test keeps one deterministic random candidate per step instead.
                var randomIndex = model.GetDeterministicRandom(available.Count, _continuousSeed);
                labs.Add(CreateLab(available[randomIndex]));
                return labs;
            }

            var index = (model.ExecutionCount - 1) % available.Count;
            labs.Add(CreateLab(available[index]));
            return labs;
        }

        private Lab<BlackboxTableModel> CreateLab(TableScenario scenario)
        {
            return new Lab<BlackboxTableModel>(
                Name + "_" + scenario.ID,
                actor: (model, _) => scenario.Execute(model));
        }
    }

    internal sealed class BlackboxTableModel : Model
    {
        public readonly List<string> NormalLogs = new List<string>();
        public readonly List<string> WarningLogs = new List<string>();
        public readonly List<string> TempDirectories = new List<string>();

        private int _ownerIndex;

        public bool Printed;
        public string ProjectName;

        public void Ignite(string projectName)
        {
            BlackboxHandle.ForceReset();

            ProjectName = projectName;
            Subject = projectName;
            Printed = false;
            _ownerIndex = 0;

            NormalLogs.Clear();
            WarningLogs.Clear();
            TempDirectories.Clear();

            BlackboxHandle.Configure(
                null,
                NormalLogs.Add,
                WarningLogs.Add,
                false,
                ExportFormat.Html,
                FullExportOption.Full,
                OpenLogOption.Never,
                ExceptionHandlingOption.None,
                TargetTypes.Full);

            BlackboxHandle.MaxLogCount = 100;
            BlackboxHandle.DefaultRecursionDepth = 100;
        }

        public NamedOwner NewOwner(string role)
        {
            _ownerIndex++;
            return new NamedOwner(ProjectName + "_" + role + "_" + _ownerIndex);
        }

        public Blackbox NewBlackbox(string role)
        {
            return BlackboxRegistry.GetBlackbox(NewOwner(role));
        }

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
        public readonly string ID;
        public readonly Action<BlackboxTableModel> Execute;
        public readonly Func<BlackboxTableModel, bool> CanRun;

        public TableScenario(string id, Action<BlackboxTableModel> execute, Func<BlackboxTableModel, bool> canRun)
        {
            ID = id;
            Execute = execute;
            CanRun = canRun;
        }
    }

    internal sealed class NamedOwner
    {
        private readonly string _name;

        public NamedOwner(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }
    }
}
#endif
