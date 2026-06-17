using System;

namespace BlackThunder.BlackboxSystem.Samples.NativeCSharp
{
    // This sample shows the smallest Blackbox flow: configure logging, create one
    // player, record its construction, and use scopes plus writes to explain how
    // HP changes over time before exporting the player's history.
    internal static class Sample_1_Write
    {
        public static void Run()
        {
            SampleEnvironment.Configure("1-Write");

            var player = new Player("Rin", 100);

            player.TakeDamage(10);
            player.TakeDamage(20);
            player.Heal(15);
            player.TakeDamage(30);

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

                // Construct runs after field assignment so ToString() and the initial state are ready.
                using var _ = BlackboxHandle.Of(this).Construct();
            }

            public void TakeDamage(int damage)
            {
                // Scope groups the damage handling steps into one log range.
                using var _ = BlackboxHandle.Of(this).Scope($"Damaged (HP={HP}, damage={damage})");

                // Write records the HP change inside the current scope.
                BlackboxHandle.Of(this).Write($"HP Change: {HP} -> {Math.Max(0, HP - damage)}");
                HP = Math.Max(0, HP - damage);
            }

            public void Heal(int heal)
            {
                // Scope groups the healing steps into one log range.
                using var _ = BlackboxHandle.Of(this).Scope($"Healed (HP={HP}, heal={heal})");

                // Write records the HP change before the value is updated.
                BlackboxHandle.Of(this).Write($"HP Change: {HP} -> {HP + heal}");
                HP += heal;
                Shine();
            }

            private void Shine()
            {
                // Nested scopes show smaller work inside the parent flow.
                using var _ = BlackboxHandle.Of(this).Scope("Shine");
                Console.WriteLine("Shine!");
            }

            public override string ToString() => Name;
        }
    }
}
