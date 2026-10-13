using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Records anonymous gameplay stats (see GameRecord): a game's row is
// created by its first guess and updated by every later guess and by the
// hint. Stats are a side record, so they can never break a game: a failure
// is logged and play carries on. Registered Scoped (depends on
// GameDbContext, sharing the request's context with GameService, which has
// saved its own changes by the time these run).
public sealed class GameStatsService : IGameStatsService
{
    private readonly GameDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameStatsService> _logger;

    public GameStatsService(GameDbContext db, TimeProvider timeProvider, ILogger<GameStatsService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RecordGuessAsync(GameGuessStat guess)
    {
        // Games started before stats existed carry no game ID.
        if (guess.GameId == Guid.Empty)
            return;

        GameRecord? record = null;
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (guess.GuessNumber == 1)
            {
                record = new GameRecord
                {
                    Id = guess.GameId,
                    SportId = guess.SportId,
                    Mode = guess.Mode,
                    PuzzleDate = guess.PuzzleDate,
                    // Started on an earlier day's puzzle: a Past Challenge.
                    IsPastChallenge = guess.PuzzleDate < DateOnly.FromDateTime(now),
                    MysteryPlayerId = guess.MysteryPlayerId,
                    GuessCount = 1,
                    StartedAt = now,
                    LastActivityAt = now
                };
                SetResult(record, guess, now);
                _db.GameRecords.Add(record);
            }
            else
            {
                // Updates only the fields a guess changes, in one statement,
                // without reading the row first.
                record = new GameRecord { Id = guess.GameId, GuessCount = guess.GuessNumber, LastActivityAt = now };
                SetResult(record, guess, now);
                var entry = _db.GameRecords.Attach(record);
                entry.Property(r => r.GuessCount).IsModified = true;
                entry.Property(r => r.LastActivityAt).IsModified = true;
                if (guess.GameOver)
                {
                    entry.Property(r => r.Result).IsModified = true;
                    entry.Property(r => r.EndedAt).IsModified = true;
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // No row to update: the game's first guess wasn't recorded (it
            // failed, or the game began before a deploy). Nothing to fix up.
            _logger.LogDebug("No stats row for game {GameId}; skipping.", guess.GameId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't record stats for game {GameId}; the game is unaffected.", guess.GameId);
        }
        finally
        {
            Forget(record);
        }
    }

    public async Task RecordHintAsync(Guid gameId, int guessesSoFar)
    {
        if (gameId == Guid.Empty)
            return;

        GameRecord? record = null;
        try
        {
            record = await _db.GameRecords.FindAsync(gameId);
            if (record == null)
                return;

            // Only the first time counts, if the hint is fetched again.
            record.HintTakenAtGuess ??= guessesSoFar;
            record.LastActivityAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't record the hint for game {GameId}; the game is unaffected.", gameId);
        }
        finally
        {
            Forget(record);
        }
    }

    private static void SetResult(GameRecord record, GameGuessStat guess, DateTime now)
    {
        if (!guess.GameOver)
            return;

        record.Result = guess.Won ? GameResult.Won : GameResult.Lost;
        record.EndedAt = now;
    }

    // Never leaves a stats row tracked in the request's context, saved or
    // not, so a failure here can't leak into anything saved later.
    private void Forget(GameRecord? record)
    {
        if (record != null)
            _db.Entry(record).State = EntityState.Detached;
    }
}
