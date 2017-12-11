using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Client.Tests
{
    public class HttpConnectionTests
    {
        public class ConnectionStateChanges
        {
            [Fact]
            public async Task CannotStartRunningConnection()
            {
                var httpHandler = new TestHttpMessageHandler();

                var connection = new HttpConnection(new Uri("http://fakeuri.org/"), TransportType.LongPolling, loggerFactory: null,
                    httpOptions: new HttpOptions { HttpMessageHandler = httpHandler });
                try
                {
                    await connection.StartAsync();
                    var exception =
                        await Assert.ThrowsAsync<InvalidOperationException>(
                            async () => await connection.StartAsync());
                    Assert.Equal("Cannot start a connection that is not in the Disconnected state.", exception.Message);
                }
                finally
                {
                    await connection.DisposeAsync();
                }
            }

        }
    }
}
