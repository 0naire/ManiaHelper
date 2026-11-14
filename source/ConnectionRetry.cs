using System;

internal sealed class ConnectionRetry {
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(12);
    int waitingGameId;
    int retriedGameId;
    DateTime waitingSince;

    public void Reset() {
        waitingGameId = retriedGameId = 0;
    }

    public bool ShouldRestart(int gameId, DateTime now) {
        if (gameId == 0) {
            Reset();
            return false;
        }
        if (waitingGameId != gameId) {
            waitingGameId = gameId;
            waitingSince = now;
        }
        if (retriedGameId == gameId || now - waitingSince < RetryDelay) return false;
        retriedGameId = gameId;
        return true;
    }

    public bool HasRetried(int gameId) { return gameId != 0 && retriedGameId == gameId; }
}
