using System;

internal static class ConnectionRetryChecks {
    static void Require(bool value, string label) {
        if (!value) throw new InvalidOperationException(label);
    }

    static void Main() {
        var retry = new ConnectionRetry();
        var time = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
        Require(!retry.ShouldRestart(0, time), "No game");
        Require(!retry.ShouldRestart(42, time), "Allow the game time to start");
        Require(!retry.ShouldRestart(42, time.AddSeconds(11)), "Wait the full grace period");
        Require(retry.ShouldRestart(42, time.AddSeconds(12)), "Restart once");
        Require(retry.HasRetried(42) && !retry.ShouldRestart(42, time.AddMinutes(1)), "Never loop on the same game");
        Require(!retry.ShouldRestart(43, time.AddMinutes(1)), "New game gets its own grace period");
        Require(retry.ShouldRestart(43, time.AddMinutes(1).AddSeconds(12)), "New game can recover");
        retry.Reset();
        Require(!retry.HasRetried(43) && !retry.ShouldRestart(43, time.AddMinutes(2)), "Clear retry on successful connection");
        Console.WriteLine("Connection retry checks passed.");
    }
}
