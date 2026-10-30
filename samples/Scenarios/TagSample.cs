using System;
using System.Collections.Generic;

namespace BlackThunder.BlackboxSystem.Samples.Scenarios
{
    // This sample shows how tags add related references to logs without making
    // them the main interaction path. Homes register monsters, attacks are exerted
    // to monsters, and each damaged monster tags its own home for context.
    internal static class TagSample
    {
        public static void Run(bool openLog)
        {
            using var session = BlackboxHandle.BeginSession();
            SampleEnvironment.Configure("Tag", openLog);

            var player = new Player("Rin", 100);
            var sandHouse = new MonsterHome("Sand House");
            var igloo = new MonsterHome("Igloo");
            var desertFox = new Monster("Desert Fox Monster", 40);
            var cactus = new Monster("Cactus Monster", 70);
            var yeti = new Monster("Yeti Monster", 120);

            sandHouse.Register(desertFox);
            sandHouse.Register(cactus);
            igloo.Register(yeti);

            player.Attack(desertFox, 15);
            player.Attack(cactus, 25);
            player.Attack(yeti, 35);

            BlackboxHandle.Of(player).Export();
        }

        private class Player
        {
            public string Name { get; }
            public int HP { get; private set; }

            public Player(string name, int hp)
            {
                Name = name;
                HP = hp;

                // Construct starts this object's Blackbox history.
                using var _ = BlackboxHandle.Of(this, label: Name).Construct();
            }

            public void Attack(Monster monster, int damage)
            {
                // Scope records the player-side attack flow.
                using var _ = BlackboxHandle.Of(this).Scope($"Attack (monster={monster.Name}, damage={damage})");

                // Exert links the attack call range to the monster's damage flow.
                using (BlackboxHandle.Of(this).Exert(monster, "Attack"))
                monster.TakeDamage(damage);
            }
        }

        private class Monster
        {
            public string Name { get; }
            public int HP { get; private set; }
            public MonsterHome Home { get; private set; }

            private readonly BlackboxHandle _blackbox;

            public Monster(string name, int hp)
            {
                Name = name;
                HP = hp;

                // Construct stores a reusable handle for this monster's logs.
                using var _ = BlackboxHandle.Of(this, label: Name).Construct(out _blackbox);
            }

            public void TakeDamage(int damage)
            {
                // With attaches the monster's home to this damage scope.
                using var _ = _blackbox
                    .Scope($"Damaged (HP={HP}, damage={damage}, home=%0)")
                    .With(Home);

                // Write records the HP change inside the tagged scope.
                _blackbox.Write($"HP Change: {HP} -> {Math.Max(0, HP - damage)}");
                HP = Math.Max(0, HP - damage);
            }

            public void SetHome(MonsterHome home)
            {
                // With attaches the assigned home to this log entry.
                using var _ = _blackbox.Scope("Set Home (home=%0)").With(home);
                Home = home;
            }
        }

        private class MonsterHome
        {
            public string Name { get; }
            public List<Monster> Monsters { get; } = new List<Monster>();

            private readonly BlackboxHandle _blackbox;

            public MonsterHome(string name)
            {
                Name = name;

                // Construct stores a reusable handle for this home's logs.
                using var _ = BlackboxHandle.Of(this, label: Name).Construct(out _blackbox);
            }

            public void Register(Monster monster)
            {
                // Scope records which monster is added to this home.
                using var _ = _blackbox.Scope($"Register (monster={monster.Name})");
                Monsters.Add(monster);

                // Exert links the home-side register flow to the monster update.
                using (_blackbox.Exert(monster, "Register"))
                monster.SetHome(this);
            }
        }
    }
}
