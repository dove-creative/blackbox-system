using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace com.BlackThunder.BlackboxSystem.Samples
{
    // This sample shows how tags add related references to logs without making
    // them the main interaction path. Homes register monsters, attacks are exerted
    // to monsters, and each damaged monster tags its own home for context.
    public class Sample_3_Tag : MonoBehaviour
    {
        public void Run()
        {
            // Set up Blackbox
            BlackboxHandle.Configure(
                logDirectory: Path.Combine(Application.persistentDataPath, "BlackboxSystem", "Samples"),
                normalLogger: Debug.Log,
                warningLogger: Debug.LogWarning,
                exportFormat: ExportFormat.Html,
                openLogOption: OpenLogOption.Open);

            // Create sample objects
            var player = new Player("Rin", 100);
            var sandHouse = new MonsterHome("Sand House");
            var igloo = new MonsterHome("Igloo");
            var desertFox = new Monster("Desert Fox Monster", 40);
            var cactus = new Monster("Cactus Monster", 70);
            var yeti = new Monster("Yeti Monster", 120);

            sandHouse.Register(desertFox);
            sandHouse.Register(cactus);
            igloo.Register(yeti);

            // Run the scenario
            player.Attack(desertFox, 15);
            player.Attack(cactus, 25);
            player.Attack(yeti, 35);

            // Export the collected logs
            BlackboxHandle.Of(player).Export();
        }


        public class Player
        {
            public string Name { get; }
            public int HP { get; private set; }

            public Player(string name, int hp)
            {
                Name = name;
                HP = hp;

                // Construct starts this object's Blackbox history.
                using var _ = BlackboxHandle.Of(this).Construct();
            }

            public void Attack(Monster monster, int damage)
            {
                // Scope records the player-side attack flow.
                using var _ = BlackboxHandle.Of(this).Scope($"Attack (monster={monster}, damage={damage})");

                // Exert links the attack call range to the monster's damage flow.
                using (BlackboxHandle.Of(this).Exert(monster, "Attack"))
                monster.TakeDamage(damage);
            }

            public override string ToString() => Name;
        }

        public class Monster
        {
            public string Name { get; }
            public int HP { get; private set; }
            public MonsterHome Home { get; private set; }

            private BlackboxHandle _bb;

            public Monster(string name, int hp)
            {
                Name = name;
                HP = hp;

                // Construct stores a reusable handle for this monster's logs.
                using var _ = BlackboxHandle.Of(this).Construct(out _bb);
            }

            public void TakeDamage(int damage)
            {
                // With attaches the monster's home to this damage scope.
                using var _ = _bb
                    .Scope($"Damaged (HP={HP}, damage={damage}, home=%0)")
                    .With(Home);

                // Write records the HP change inside the tagged scope.
                _bb.Write($"HP Change: {HP} -> {Mathf.Max(0, HP - damage)}");
                HP = Mathf.Max(0, HP - damage);
            }

            public void SetHome(MonsterHome home)
            {
                // With attaches the assigned home to this log entry.
                using var _ = _bb.Scope("Set Home (home=%0)").With(home);
                Home = home;
            }

            public override string ToString() => Name;
        }

        public class MonsterHome
        {
            public string Name { get; }
            public List<Monster> Monsters { get; } = new List<Monster>();

            private BlackboxHandle _bb;

            public MonsterHome(string name)
            {
                Name = name;

                // Construct stores a reusable handle for this home's logs.
                using var _ = BlackboxHandle.Of(this).Construct(out _bb);
            }

            public void Register(Monster monster)
            {
                // Scope records which monster is added to this home.
                using var _ = _bb.Scope($"Register (monster={monster})");
                Monsters.Add(monster);

                // Exert links the home-side register flow to the monster update.
                using (_bb.Exert(monster, "Register"))
                monster.SetHome(this);
            }

            public override string ToString() => Name;
        }

    }
}
