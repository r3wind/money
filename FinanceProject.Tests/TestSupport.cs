using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FinanceProject.Models;
using FinanceProject.Services;
using Microsoft.JSInterop;

namespace FinanceProject.Tests;

internal sealed record RegressionCase(string Name, Func<Task> Run)
{
    public static RegressionCase Sync(string name, Action run)
        => new(name, () => { run(); return Task.CompletedTask; });
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected <{expected}>, actual <{actual}>.");
    }

    public static void True(bool actual, string message) => Equal(true, actual, message);
    public static void False(bool actual, string message) => Equal(false, actual, message);

    public static void Pot(SavingsSubPot pot, decimal amount, decimal rate, DateOnly? month)
    {
        Equal(amount, pot.Amount, $"{pot.Name} amount");
        Equal(rate, pot.MonthlyContribution, $"{pot.Name} rate");
        Equal(month, pot.LastContributionMonth, $"{pot.Name} tracking month");
    }

    public static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}

internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MutableTimeProvider(int year, int month, int day) => Set(year, month, day);
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Set(int year, int month, int day)
        => _utcNow = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeStorageRuntime : IJSRuntime
{
    public Dictionary<string, string> Storage { get; } = new(StringComparer.Ordinal);
    public List<string> SavedKeys { get; } = [];
    public List<string> LoadedKeys { get; } = [];

    public void Seed<T>(string key, T value) => Storage[key] = JsonSerializer.Serialize(value);

    public T Read<T>(string key)
        => JsonSerializer.Deserialize<T>(Storage[key])
            ?? throw new InvalidOperationException($"Storage key {key} contained null.");

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (args is null || args.Length == 0 || args[0] is not string key)
            throw new InvalidOperationException($"Invalid arguments to {identifier}.");

        switch (identifier)
        {
            case "firebaseInterop.firestoreSave" when args.Length == 2 && args[1] is string json:
                using (JsonDocument.Parse(json)) { }
                Storage[key] = json;
                SavedKeys.Add(key);
                return ValueTask.FromResult(default(TValue)!);
            case "firebaseInterop.firestoreLoad" when args.Length == 1:
                LoadedKeys.Add(key);
                Storage.TryGetValue(key, out var storedJson);
                return ValueTask.FromResult(storedJson is null ? default! : (TValue)(object)storedJson);
            default:
                throw new InvalidOperationException($"Unexpected JS interop call: {identifier}.");
        }
    }
}

internal sealed class StateFixture
{
    public const string SubPotsKey = "finance_savingssubpots";
    public FakeStorageRuntime Storage { get; } = new();
    public MutableTimeProvider Clock { get; }
    public FinanceStateService State { get; }

    public StateFixture(int year = 2024, int month = 1, int day = 15)
    {
        Clock = new MutableTimeProvider(year, month, day);
        State = FreshState();
    }

    public FinanceStateService FreshState() => new(new FirestoreService(Storage), Clock);
    public List<SavingsSubPot> SavedPots => Storage.Read<List<SavingsSubPot>>(SubPotsKey);
    public void Seed(params SavingsSubPot[] pots) => Storage.Seed(SubPotsKey, pots);

    public void AssertOnlySubPotsSaved(int expectedCount)
    {
        Assert.Equal(expectedCount, Storage.SavedKeys.Count, "Number of saves");
        Assert.True(Storage.SavedKeys.All(key => key == SubPotsKey), "Only sub-pots storage should be written");
    }
}
