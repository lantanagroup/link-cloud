using LantanaGroup.Link.Normalization.Domain;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class SqlDeadlockTests
{
    [Fact]
    public async Task ReadRetriesDeadlockThenReturns()
    {
        var attempts = 0;
        var result = await SqlDeadlock.ExecuteReadAsync<int>(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("Transaction (Process ID 51) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.");
            }

            return Task.FromResult(7);
        }, CancellationToken.None);

        Assert.Equal(7, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ReadDoesNotRetryOtherFailures()
    {
        var attempts = 0;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SqlDeadlock.ExecuteReadAsync<int>(() =>
        {
            attempts++;
            throw new InvalidOperationException("connection failed");
        }, CancellationToken.None));

        Assert.Equal("connection failed", exception.Message);
        Assert.Equal(1, attempts);
    }
}
