using System;

namespace BlackThunder.BlackboxSystem.Samples.NativeCSharp
{
    // This sample shows an interaction between two objects. The player records the
    // drink flow on itself, Exert connects that flow to the potion, and the potion
    // records its own scope so the exported log can read both sides together.
    internal static class Sample_2_Exert
    {
        public static void Run()
        {
            SampleEnvironment.Configure("2-Exert");

            var player = new Player("Rin", 100);
            var potion = new Potion("Sunshine Potion", 15);

            player.TakeDamage(10);
            player.TakeDamage(20);
            player.Drink(potion);
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

                // Construct starts this object's Blackbox history.
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

            public void Drink(Potion potion)
            {
                // Scope records the player's full drink flow.
                using var _ = BlackboxHandle.Of(this).Scope($"Drink (HP={HP}, Potion={potion})");
                int heal;

                // Exert links this call range to the potion's own behavior.
                using (BlackboxHandle.Of(this).Exert(potion, "Drink Potion"))
                heal = potion.Drink();

                // Write records the result returned from the exerted object.
                BlackboxHandle.Of(this).Write($"Drink Potion: Got {heal}");
                BlackboxHandle.Of(this).Write($"HP Change: {HP} -> {HP + heal}");
                HP += heal;
            }

            public override string ToString() => Name;
        }

        private class Potion
        {
            public string Name { get; }
            public int HealAmount { get; private set; }

            public Potion(string name, int healAmount)
            {
                Name = name;
                HealAmount = healAmount;

                // Construct gives the potion its own Blackbox history.
                using var _ = BlackboxHandle.Of(this).Construct();
            }

            public int Drink()
            {
                // The receiving object records its work inside the exert range.
                using var _ = BlackboxHandle.Of(this).Scope("Drink");

                // Write records the potion state change.
                BlackboxHandle.Of(this).Write($"Consumed: {HealAmount} -> 0");
                var healAmount = HealAmount;
                HealAmount = 0;

                return healAmount;
            }

            public override string ToString() => Name;
        }
    }
}
