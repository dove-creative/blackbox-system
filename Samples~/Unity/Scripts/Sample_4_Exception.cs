using System;
using System.IO;
using UnityEngine;

namespace BlackThunder.BlackboxSystem.Samples
{
    // This sample reuses the potion flow as a failure case. The player receives a
    // null potion, records the problem with WriteError, and then throws so the
    // crash export shows the collected history up to the failed drink.
    public class Sample_4_Exception : MonoBehaviour
    {
        public void Run()
        {
            // Set up Blackbox
            BlackboxHandle.Configure(
                logDirectory: Path.Combine(Application.persistentDataPath, "BlackboxSystem", "Samples"),
                normalLogger: Debug.Log,
                warningLogger: Debug.LogWarning,
                exportFormat: ExportFormat.Html,
                openLogOption: OpenLogOption.Open,
                exceptionHandlingOption: ExceptionHandlingOption.CrashExport); // Export the log when WriteError is called.

            // Create sample objects
            Player player = new Player("Rin", 100);
            Potion potion = null;

            // Run the scenario
            player.TakeDamage(10);
            player.TakeDamage(20);
            player.Drink(potion); // Execution stops here.
            player.TakeDamage(30);

            // This line is not reached after the exception.
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

            public void TakeDamage(int damage)
            {
                // Scope groups the damage handling steps into one log range.
                using var _ = BlackboxHandle.Of(this).Scope($"Damaged (HP={HP}, damage={damage})");

                // Write records the HP change inside the current scope.
                BlackboxHandle.Of(this).Write($"HP Change: {HP} -> {Mathf.Max(0, HP - damage)}");
                HP = Mathf.Max(0, HP - damage);
            }

            public void Drink(Potion potion)
            {
                // Scope records the drink attempt, even when it fails.
                using var _ = BlackboxHandle.Of(this).Scope($"Drink (HP={HP}, Potion={potion})");

                if (potion == null)
                {
                    // WriteError records the failure before the exception is thrown.
                    var message = BlackboxHandle.Of(this).WriteError("Potion cannot be null.");
                    throw new NullReferenceException(message);
                }

                int heal;
                // Exert links this call range to the potion's own behavior.
                using (BlackboxHandle.Of(this).Exert(potion, "Drink Potion"))
                heal = potion.Drink();

                // Write records the successful drink result.
                BlackboxHandle.Of(this).Write($"Drink Potion: Got {heal}");
                BlackboxHandle.Of(this).Write($"HP Change: {HP} -> {HP + heal}");
                HP += heal;
            }

            public override string ToString() => Name;
        }

        public class Potion
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
