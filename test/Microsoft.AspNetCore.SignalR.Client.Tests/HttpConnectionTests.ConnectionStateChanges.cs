using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Client.Tests
{
    public partial class HttpConnectionTests
    {
        public class ConnectionStateChanges
        {
            [Fact]
            public Task CannotStartRunningConnection()
            {
                return WithConnectionAsync(CreateConnection(), async (connection, closed) =>
                {
                    await connection.StartAsync();
                    var exception =
                        await Assert.ThrowsAsync<InvalidOperationException>(
                            async () => await connection.StartAsync().OrTimeout());
                    Assert.Equal("Cannot start a connection that is not in the Disconnected state.", exception.Message);
                });
            }


            [Fact]
            public async Task CannotStartConnectionDisposedAfterStartingAsync()
            {
                var connection = CreateConnection();
                await connection.StartAsync();
                await connection.DisposeAsync();
                var exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await connection.StartAsync().OrTimeout());

                Assert.Equal("Cannot start a connection that is not in the Disconnected state.", exception.Message);
            }

            [Fact]
            public async Task CannotStartDisposedConnectionAsync()
            {
                var connection = CreateConnection();
                await connection.DisposeAsync();
                var exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await connection.StartAsync().OrTimeout());

                Assert.Equal("Cannot start a connection that is not in the Disconnected state.", exception.Message);
            }

            [Fact]
            public Task CanDisposeStartingConnection()
            {
                return WithConnectionAsync(
                    CreateConnection(onTransportStart: SyncPoint.Create(out var transportStart), onTransportStop: SyncPoint.Create(out var transportStop)),
                    async (connection, closed) =>
                {
                    // Start the connection and wait for the transport to start up.
                    var startTask = connection.StartAsync();
                    await transportStart.WaitForSyncPoint().OrTimeout();

                    // While the transport is starting, dispose the connection
                    var disposeTask = connection.DisposeAsync();
                    transportStart.Continue(); // We need to release StartAsync, because Dispose waits for it.

                    // Wait for start to finish, as that has to finish before the transport will be stopped.
                    await startTask.OrTimeout();

                    // Then release DisposeAsync (via the transport StopAsync call)
                    await transportStop.WaitForSyncPoint().OrTimeout();
                    transportStop.Continue();
                });
            }

            [Fact]
            public Task CanStartConnectionThatFailedToStart()
            {
                var expected = new Exception("Transport failed to start");
                var shouldFail = true;

                Task OnTransportStart()
                {
                    if (shouldFail)
                    {
                        // Succeed next time
                        shouldFail = false;
                        return Task.FromException(expected);
                    }
                    else
                    {
                        return Task.CompletedTask;
                    }
                }

                return WithConnectionAsync(
                    CreateConnection(onTransportStart: OnTransportStart),
                    async (connection, closed) =>
                {
                    var actual = await Assert.ThrowsAsync<Exception>(() => connection.StartAsync());
                    Assert.Same(expected, actual);

                    // Should succeed this time
                    shouldFail = false;

                    await connection.StartAsync().OrTimeout();
                });
            }

            [Fact]
            public Task CanStartStoppedConnection()
            {
                return WithConnectionAsync(
                    CreateConnection(),
                    async (connection, closed) =>
                {
                    await connection.StartAsync().OrTimeout();
                    await connection.StopAsync().OrTimeout();
                    await connection.StartAsync().OrTimeout();
                });
            }

            [Fact]
            public Task CanStopStartingConnection()
            {
                return WithConnectionAsync(
                    CreateConnection(onTransportStart: SyncPoint.Create(out var transportStart)),
                    async (connection, closed) =>
                {
                    // Start and wait for the transport to start up.
                    var startTask = connection.StartAsync();
                    await transportStart.WaitForSyncPoint().OrTimeout();

                    // Stop the connection while it's starting
                    var stopTask = connection.StopAsync();
                    transportStart.Continue(); // We need to release Start in order for Stop to begin working.

                    // Wait for start to finish, which will allow stop to finish and the connection to close.
                    await startTask.OrTimeout();
                    await stopTask.OrTimeout();
                    await closed.OrTimeout();
                });
            }
        }
    }
}
