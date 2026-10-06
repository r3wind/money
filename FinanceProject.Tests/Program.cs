using FinanceProject.Tests;

var tests = ModelRegressionTests.Cases()
    .Concat(StateRegressionTests.Cases())
    .Concat(UpcomingCostRegressionTests.Cases())
    .Concat(UpcomingCostRegressionTests.ForecastCases())
    .ToArray();
var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
    }
}

Console.WriteLine($"Results: {tests.Length - failed} passed, {failed} failed, {tests.Length} total.");
return failed == 0 ? 0 : 1;
